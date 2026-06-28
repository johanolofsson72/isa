using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Agent.Services
{
    public class McpWebSocketServer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly int _port;
        private readonly Dictionary<string, ChatHistory> _sessions = new();
        private readonly object _lock = new();
        private List<JsonNode>? _cachedPluginCapabilities = null;
        private readonly object _capLock = new();
        private readonly string _pluginDirectory;
        private readonly McpService _mcpService;

        public McpWebSocketServer(IServiceProvider serviceProvider, int port = 5100)
        {
            _serviceProvider = serviceProvider;
            _port = port;
            var config = serviceProvider.GetService<IConfiguration>();
            _pluginDirectory = config?["Mcp:PluginDirectory"] ?? string.Empty;
            _mcpService = serviceProvider.GetRequiredService<McpService>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{_port}/ws/");
            listener.Start();
            Console.WriteLine($"[MCP] WebSocket server listening on port {_port}");

            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    _ = HandleConnection(wsContext.WebSocket, stoppingToken);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private async Task HandleConnection(WebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[4096];
            string sessionId = Guid.NewGuid().ToString();
            Console.WriteLine($"[MCP] New WebSocket connection: {sessionId}");
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", ct);
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var response = await _mcpService.HandleMcpRequestAsync(json, sessionId);
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await socket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, ct);
            }
            lock (_lock)
            {
                _sessions.Remove(sessionId);
            }
            Console.WriteLine($"[MCP] Connection closed: {sessionId}");
        }

        private List<JsonNode> LoadAllPluginCapabilities()
        {
            var plugins = new List<JsonNode>();
            string srcDir = _pluginDirectory;
            if (string.IsNullOrWhiteSpace(srcDir))
            {
                srcDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src");
            }
            if (!Directory.Exists(srcDir))
            {
                Console.WriteLine($"[MCP] Plugin directory not found: {srcDir}");
                return plugins;
            }
            foreach (var dir in Directory.GetDirectories(srcDir, "*Plugin"))
            {
                var capFile = Path.Combine(dir, "plugin.capabilities");
                if (File.Exists(capFile))
                {
                    try
                    {
                        var json = File.ReadAllText(capFile);
                        var node = JsonNode.Parse(json);
                        if (node != null) plugins.Add(node);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MCP] Fel vid inläsning av {capFile}: {ex.Message}");
                    }
                }
            }
            return plugins;
        }

        private List<JsonNode> GetCachedPluginCapabilities()
        {
            lock (_capLock)
            {
                if (_cachedPluginCapabilities == null)
                {
                    _cachedPluginCapabilities = LoadAllPluginCapabilities();
                }
                return _cachedPluginCapabilities;
            }
        }
    }
} 