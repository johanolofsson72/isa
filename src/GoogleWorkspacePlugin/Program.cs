using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GoogleWorkspace;
using System.Text.Json;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
        if (args != null)
            config.AddCommandLine(args);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<Plugin>();
        services.AddLogging(cfg => cfg.AddConsole());
    });

var app = builder.Build();
var plugin = app.Services.GetRequiredService<Plugin>();

// HTTP channel
var webApp = WebApplication.CreateBuilder(args);
webApp.Services.AddSingleton(plugin);
webApp.Services.AddLogging(cfg => cfg.AddConsole());
var web = webApp.Build();

web.MapGet("/health", () => "OK");
web.MapGet("/drive/list", async (string? folder, Plugin plugin) => await plugin.ListFiles(folder));
web.MapPost("/drive/upload", async (string fileName, string content, string? folder, Plugin plugin) => await plugin.UploadFile(fileName, content, folder));
web.MapGet("/drive/read", async (string fileName, string? folder, Plugin plugin) => await plugin.ReadFile(fileName, folder));
web.MapPost("/calendar/create", async (string title, string start, string end, string? desc, Plugin plugin) => await plugin.CreateCalendarEvent(title, start, end, desc));
web.MapGet("/calendar/list", async (int? max, Plugin plugin) => await plugin.ListCalendarEvents(max ?? 10));
web.MapPost("/chat/send", async (string to, string message, Plugin plugin) => await plugin.SendChatMessage(to, message));

// Start HTTP server in background
var httpTask = web.RunAsync("http://0.0.0.0:5509");

// IO channel (stdin/stdout loop)
Console.WriteLine("GoogleWorkspacePlugin IO channel ready. Type JSON requests or 'exit'.");
string? line;
while ((line = Console.ReadLine()) != null)
{
    if (line.Trim().ToLower() == "exit") break;
    // Expecting JSON: { "method": "drive.list", "parameters": { ... } }
    try
    {
        var req = System.Text.Json.JsonDocument.Parse(line);
        var method = req.RootElement.GetProperty("method").GetString();
        var parameters = req.RootElement.TryGetProperty("parameters", out var p) ? p : default(JsonElement);
        string result = method switch
        {
            "drive.list" => await plugin.ListFiles(parameters.ValueKind != JsonValueKind.Undefined ? parameters.GetProperty("folder").GetString() : null),
            "drive.upload" => await plugin.UploadFile(
                parameters.GetProperty("fileName").GetString() ?? string.Empty,
                parameters.GetProperty("content").GetString() ?? string.Empty,
                parameters.TryGetProperty("folder", out var f) ? f.GetString() : null),
            "drive.read" => await plugin.ReadFile(
                parameters.GetProperty("fileName").GetString() ?? string.Empty,
                parameters.TryGetProperty("folder", out var rf) ? rf.GetString() : null),
            "calendar.create" => await plugin.CreateCalendarEvent(
                parameters.GetProperty("title").GetString() ?? string.Empty,
                parameters.GetProperty("start").GetString() ?? string.Empty,
                parameters.GetProperty("end").GetString() ?? string.Empty,
                parameters.TryGetProperty("desc", out var d) ? d.GetString() : null),
            "calendar.list" => await plugin.ListCalendarEvents(
                parameters.TryGetProperty("max", out var m) ? m.GetInt32() : 10),
            "calendar.search" => await plugin.SearchCalendarEvents(
                parameters.TryGetProperty("title", out var t) ? t.GetString() : null,
                parameters.TryGetProperty("startDate", out var sd) ? sd.GetString() : null,
                parameters.TryGetProperty("endDate", out var ed) ? ed.GetString() : null,
                parameters.TryGetProperty("max", out var sm) ? sm.GetInt32() : 50),
            "chat.send" => await plugin.SendChatMessage(
                parameters.GetProperty("to").GetString() ?? string.Empty,
                parameters.GetProperty("message").GetString() ?? string.Empty),
            _ => $"ERROR: Unknown method '{method}'"
        };
        Console.WriteLine(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}

await app.StopAsync(); 