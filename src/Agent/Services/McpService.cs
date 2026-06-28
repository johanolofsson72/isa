using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Agent.Services
{
    public class McpService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, ChatHistory> _sessions = new();
        private readonly object _lock = new();
        private readonly DynamicMcpCapabilitiesGenerator _capabilitiesGenerator;
        private readonly McpClientDiscoveryService _clientDiscovery;
        private readonly ILogger<McpService> _logger;
        private readonly IMcpStreamingService _streamingService;
        private readonly IMcpProgressService _progressService;
        private readonly IConfiguration _configuration;
        private readonly McpToolMappingGenerator _toolMappingGenerator;



        public McpService(
            IServiceProvider serviceProvider,
            IConfiguration config,
            DynamicMcpCapabilitiesGenerator capabilitiesGenerator,
            McpClientDiscoveryService clientDiscovery,
            ILogger<McpService> logger,
            IMcpStreamingService streamingService,
            IMcpProgressService progressService,
            McpToolMappingGenerator toolMappingGenerator)
        {
            _serviceProvider = serviceProvider;
            _configuration = config;
            _capabilitiesGenerator = capabilitiesGenerator;
            _clientDiscovery = clientDiscovery;
            _logger = logger;
            _streamingService = streamingService;
            _progressService = progressService;
            _toolMappingGenerator = toolMappingGenerator;
        }

        public async Task<string> HandleMcpRequestAsync(string json, string sessionId, string? userAgent = null, string? origin = null)
        {
            try
            {
                // Register client if not already registered
                if (!string.IsNullOrEmpty(userAgent))
                {
                    _clientDiscovery.RegisterClient(sessionId, userAgent, origin);
                }

                // Update client activity
                _clientDiscovery.UpdateClientActivity(sessionId);

                using var scope = _serviceProvider.CreateScope();
                var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Handle id as either string or number according to JSON-RPC spec
                var idProperty = root.GetProperty("id");
                var id = idProperty.ValueKind == JsonValueKind.String
                    ? idProperty.GetString()
                    : idProperty.GetRawText();

                var method = root.GetProperty("method").GetString();

                // Handle optional params property
                var hasParams = root.TryGetProperty("params", out var @params);

                // MCP Standard Methods
                if (method == "initialize")
                {
                    _logger.LogInformation("🔧 MCP: Handling initialize request");

                    var initializeResponse = new
                    {
                        id = id,
                        result = new
                        {
                            protocolVersion = "2024-11-05",
                            capabilities = new
                            {
                                tools = new { },
                                resources = new { },
                                prompts = new { },
                                logging = new { }
                            },
                            serverInfo = new
                            {
                                name = "ISA Accounting Agent",
                                version = "1.0.0"
                            }
                        }
                    };
                    return JsonSerializer.Serialize(initializeResponse);
                }
                else if (method == "initialized")
                {
                    _logger.LogInformation("🔧 MCP: Handling initialized notification");
                    // This is a notification, no response needed
                    return "{}";
                }
                else if (method == "tools/list")
                {
                    _logger.LogInformation("🔧 MCP: Handling tools/list request - returning hybrid tools (all tools + execute_task)");

                    var toolsList = new List<object>();

                    // Try to load dynamic tool mapping for individual tools
                    try
                    {
                        var toolMapping = await _toolMappingGenerator.LoadToolMappingAsync();
                        _logger.LogInformation("📋 Tool mapping loaded: {HasMapping}, Tools: {ToolCount}",
                            toolMapping != null, toolMapping?.Tools?.Count ?? 0);

                        // Add all individual tools if mapping is available
                        if (toolMapping?.Tools != null && toolMapping.Tools.Count > 0)
                        {
                            var individualTools = toolMapping.Tools.Values.Select(tool => new
                            {
                                name = tool.ToolName,
                                description = tool.Description,
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = tool.Parameters.ToDictionary(
                                        p => p.Name,
                                        p => new
                                        {
                                            type = p.Type,
                                            description = p.Description
                                        }
                                    ),
                                    required = tool.Parameters.Where(p => p.Required).Select(p => p.Name).ToArray()
                                }
                            });
                            toolsList.AddRange(individualTools);
                            _logger.LogInformation("✅ Added {Count} individual tools from mapping", toolMapping.Tools.Count);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ No tools found in mapping, will generate tools dynamically");
                            // Fallback: Generate tools dynamically from Kernel
                            var dynamicTools = await GenerateToolsFromKernelAsync();
                            toolsList.AddRange(dynamicTools);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Failed to load tool mapping, generating tools dynamically");
                        // Fallback: Generate tools dynamically from Kernel
                        var dynamicTools = await GenerateToolsFromKernelAsync();
                        toolsList.AddRange(dynamicTools);
                    }

                    // Add the intelligent execute_task tool
                    toolsList.Add(new
                    {
                        name = "execute_task",
                        description = @"🤖 ISA (Intelligent System Agent) - En autonom AI-assistent som kan utföra komplexa uppgifter baserat på naturlig språkbeskrivning.

KAPACITETER:
• 📧 E-post: Skicka/läsa mail via Mailjet och IMAP/POP3
• 📱 SMS: Skicka SMS via Twilio
• 💬 Kommunikation: Slack (meddelanden, DM), Microsoft Teams, Google Chat
• ☁️ Molnlagring: Dropbox (lista, ladda upp/ner, skapa mappar, flytta filer), OneDrive
• 📅 Kalender: Microsoft Calendar (skapa/uppdatera/radera händelser)
• 🌤️ Väder: Aktuellt väder och prognoser (OpenMeteo)
• 🗺️ Geokodning: Adresser ↔ koordinater (OpenCage)
• 🔍 Webbsökning: Google Custom Search, personsökning
• 🕐 Datum/tid: Aktuell tid/datum baserat på plats
• 📊 Google Workspace: Gmail, Drive, Calendar integration

EXEMPEL: 'Hämta vädret för imorgon i Torrevieja och skicka till Anna via mail'

ISA analyserar uppgiften, planerar stegen och utför allt autonomt.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new Dictionary<string, object>
                            {
                                ["task_description"] = new
                                {
                                    type = "string",
                                    description = "Beskriv vad du vill att ISA ska göra i naturligt språk. ISA kan hantera e-post, SMS, Slack, Teams, Dropbox, OneDrive, kalender, väder, geokodning, webbsökning och mycket mer."
                                },
                                ["user_context"] = new
                                {
                                    type = "string",
                                    description = "Valfri kontextuell information som kan hjälpa ISA att utföra uppgiften bättre"
                                }
                            },
                            required = new[] { "task_description" }
                        }
                    });

                    var mcpResponse = new
                    {
                        id = id,
                        result = new { tools = toolsList.ToArray() }
                    };

                    _logger.LogInformation("📋 Returning {Count} tools in hybrid mode (individual tools + execute_task)", toolsList.Count);

                    return JsonSerializer.Serialize(mcpResponse);
                }
                else if (method == "tools/call")
                {
                    _logger.LogInformation("🔧 MCP: Handling tools/call request");
                    if (!hasParams)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            id,
                            error = new { code = -32602, message = "Missing required 'params' property for tools/call" }
                        });
                    }

                    var toolName = @params.GetProperty("name").GetString();
                    var arguments = @params.TryGetProperty("arguments", out var args) ? args : new JsonElement();

                    // Implement actual tool calling
                    var toolResult = await CallToolAsync(toolName, arguments);

                    var toolResponse = new
                    {
                        id = id,
                        result = new
                        {
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = toolResult
                                }
                            }
                        }
                    };
                    return JsonSerializer.Serialize(toolResponse);
                }
                else if (method == "chat.completions.create")
                {
                    if (!hasParams)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            id,
                            error = new { code = -32602, message = "Missing required 'params' property for chat.completions.create" }
                        });
                    }

                    // Check if streaming is requested
                    var isStreaming = @params.TryGetProperty("stream", out var streamProp) && streamProp.GetBoolean();

                    if (isStreaming)
                    {
                        // For streaming, we need to return a different response format
                        // This is a simplified implementation - real streaming would use Server-Sent Events
                        return await HandleStreamingChatAsync(id?.ToString() ?? string.Empty, @params, sessionId);
                    }

                    var messages = @params.GetProperty("messages").EnumerateArray();
                    ChatHistory history;
                    lock (_lock)
                    {
                        if (!_sessions.TryGetValue(sessionId, out history!))
                        {
                            history = new ChatHistory();
                            _sessions[sessionId] = history;
                        }
                    }
                    // Inject system-prompt.txt if not already present
                    bool hasSystem = false;
                    foreach (var msg in messages)
                    {
                        var role = msg.GetProperty("role").GetString();
                        var content = msg.GetProperty("content").GetString();
                        if (role == "system")
                        {
                            hasSystem = true;
                            history.AddSystemMessage(content ?? string.Empty);
                        }
                        else if (role == "user")
                            history.AddUserMessage(content ?? string.Empty);
                        else if (role == "assistant")
                            history.AddAssistantMessage(content ?? string.Empty);
                    }
                    if (!hasSystem)
                    {
                        try
                        {
                            // Use dynamic system prompt generator
                            var systemPromptGenerator = scope.ServiceProvider.GetRequiredService<IDynamicSystemPromptGenerator>();
                            var sysPrompt = await systemPromptGenerator.GenerateSystemPromptAsync();
                            history.AddSystemMessage(sysPrompt);
                            Console.WriteLine($"[MCP] Loaded dynamic system prompt ({sysPrompt.Length} chars)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MCP] Failed to generate dynamic system prompt: {ex.Message}");
                            // Fallback to basic prompt
                            history.AddSystemMessage("You are a helpful AI assistant.");
                        }
                    }
                    // Use CancellationToken with longer timeout for complex operations
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                    try
                    {
                        // Use the new streaming service for better handling of complex operations
                        var reply = await _streamingService.ProcessStreamingChatAsync(
                            history,
                            kernel,
                            sessionId,
                            cts.Token);

                        history.AddAssistantMessage(reply);

                        var mcpResponse = new
                        {
                            id = id,
                            result = new
                            {
                                choices = new[] {
                                    new {
                                        message = new {
                                            content = reply,
                                            role = "assistant"
                                        }
                                    }
                                }
                            }
                        };
                        return JsonSerializer.Serialize(mcpResponse);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("🕐 MCP: Operation timed out after 5 minutes");
                        var timeoutResponse = new
                        {
                            id = id,
                            result = new
                            {
                                choices = new[] {
                                    new {
                                        message = new {
                                            content = "⏰ Operationen tog för lång tid och avbröts. Försök med en enklare fråga eller dela upp den i mindre delar.",
                                            role = "assistant"
                                        }
                                    }
                                }
                            }
                        };
                        return JsonSerializer.Serialize(timeoutResponse);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ MCP: Error processing chat completion");
                        var errorResponse = new
                        {
                            id = id,
                            result = new
                            {
                                choices = new[] {
                                    new {
                                        message = new {
                                            content = $"❌ Ett fel uppstod: {ex.Message}. Försök igen eller kontakta support.",
                                            role = "assistant"
                                        }
                                    }
                                }
                            }
                        };
                        return JsonSerializer.Serialize(errorResponse);
                    }
                }
                else if (method == "server.capabilities")
                {
                    _logger.LogInformation("🔧 MCP: Handling server.capabilities request");

                    var mcpResponse = new
                    {
                        id = id,
                        result = new
                        {
                            capabilities = new
                            {
                                tools = new { },
                                resources = new { },
                                prompts = new { },
                                logging = new { }
                            }
                        }
                    };
                    return JsonSerializer.Serialize(mcpResponse);
                }
                else if (method == "tools/list")
                {
                    _logger.LogInformation("🔧 MCP: Handling tools/list request (duplicate)");

                    var mcpResponse = new
                    {
                        id = id,
                        result = new
                        {
                            tools = new[]
                            {
                                new
                                {
                                    name = "send_email",
                                    description = "Send an email using the Mail plugin",
                                    inputSchema = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            to = new { type = "string", description = "Recipient email address" },
                                            subject = new { type = "string", description = "Email subject" },
                                            body = new { type = "string", description = "Email body content" }
                                        },
                                        required = new[] { "to", "subject", "body" }
                                    }
                                }
                            }
                        }
                    };
                    return JsonSerializer.Serialize(mcpResponse);
                }
                else if (method == "progress/status")
                {
                    _logger.LogInformation("🔧 MCP: Handling progress/status request");
                    if (!hasParams)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            id,
                            error = new { message = "Missing required 'params' property for progress/status" }
                        });
                    }

                    var progressId = @params.GetProperty("progressId").GetString();
                    if (string.IsNullOrEmpty(progressId))
                    {
                        return JsonSerializer.Serialize(new
                        {
                            id,
                            error = new { message = "Missing progressId parameter" }
                        });
                    }

                    var status = await _progressService.GetProgressStatusAsync(progressId);
                    var mcpResponse = new
                    {
                        id = id,
                        result = new
                        {
                            status = status ?? "Progress not found"
                        }
                    };
                    return JsonSerializer.Serialize(mcpResponse);
                }
                else
                {
                    return JsonSerializer.Serialize(new
                    {
                        id = id,
                        error = new { message = $"Unknown method: {method}" }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MCP] Error: {ex.Message}\n{ex.StackTrace}");
                return JsonSerializer.Serialize(new
                {
                    id = Guid.NewGuid().ToString(),
                    error = new { message = $"Exception: {ex.Message}" }
                });
            }
        }

        /// <summary>
        /// Calls a specific tool with the provided arguments
        /// </summary>
        private async Task<string> CallToolAsync(string? toolName, JsonElement arguments)
        {
            try
            {
                if (string.IsNullOrEmpty(toolName))
                {
                    return "Error: Tool name is required";
                }

                _logger.LogInformation("🔧 MCP: Calling tool '{ToolName}' with arguments", toolName);

                // Handle the intelligent execute_task tool
                if (toolName.Equals("execute_task", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExecuteTaskAsync(arguments);
                }

                // Handle individual tools (legacy compatibility)
                return await CallIndividualToolAsync(toolName, arguments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling tool '{ToolName}'", toolName);
                return $"Error calling tool '{toolName}': {ex.Message}";
            }
        }

        /// <summary>
        /// Executes a task autonomously using the same ChatExecutionService as WebChat
        /// </summary>
        private async Task<string> ExecuteTaskAsync(JsonElement arguments)
        {
            try
            {
                // Extract task description and optional context
                if (!arguments.TryGetProperty("task_description", out var taskDescElement))
                {
                    return "Error: task_description is required";
                }

                var taskDescription = taskDescElement.GetString();
                if (string.IsNullOrWhiteSpace(taskDescription))
                {
                    return "Error: task_description cannot be empty";
                }

                var userContext = arguments.TryGetProperty("user_context", out var contextElement)
                    ? contextElement.GetString() ?? ""
                    : "";

                _logger.LogInformation("🤖 ISA: Executing autonomous task: {TaskDescription}", taskDescription);

                // Create a scope to get scoped services
                using var scope = _serviceProvider.CreateScope();
                var chatExecutionService = scope.ServiceProvider.GetRequiredService<ChatExecutionService>();
                var chatHistoryFactory = scope.ServiceProvider.GetRequiredService<IChatHistoryFactory>();

                // Create a fresh chat history for this MCP request
                var chatHistory = await chatHistoryFactory.CreateWebChatHistoryAsync();

                // Create the user message with context if provided
                var fullMessage = string.IsNullOrWhiteSpace(userContext)
                    ? taskDescription
                    : $"{taskDescription}\n\nYtterligare kontext: {userContext}";

                // Add user message to history
                await chatHistoryFactory.AddUserMessageAsync(chatHistory, fullMessage);

                // Execute using the same proven method as WebChat
                var response = await chatExecutionService.ExecuteChatCompletionAsync(
                    chatHistory,
                    sessionId: "mcp-session",
                    useCache: true);

                _logger.LogInformation("✅ ISA: Task completed successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ISA: Error executing autonomous task");
                return $"Fel vid utförande av uppgift: {ex.Message}";
            }
        }

        /// <summary>
        /// Handles individual tool calls (legacy compatibility)
        /// </summary>
        private async Task<string> CallIndividualToolAsync(string toolName, JsonElement arguments)
        {
            try
            {
                _logger.LogInformation("🔧 Calling individual tool: {ToolName}", toolName);

                // Create a scope to get scoped services
                using var scope = _serviceProvider.CreateScope();
                var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();

                // Parse tool name to get plugin and function names
                var normalizedToolName = toolName.ToLowerInvariant();
                var parts = normalizedToolName.Split('_', 2);

                if (parts.Length != 2)
                {
                    return $"Error: Invalid tool name format '{toolName}'. Expected format: 'pluginname_functionname'";
                }

                var pluginName = parts[0];
                var functionName = parts[1];

                // Find the plugin and function directly in Kernel
                KernelFunction? targetFunction = null;
                try
                {
                    // Try different plugin name variations
                    var possiblePluginNames = new[]
                    {
                        pluginName,
                        char.ToUpper(pluginName[0]) + pluginName.Substring(1),
                        pluginName + "Plugin",
                        char.ToUpper(pluginName[0]) + pluginName.Substring(1) + "Plugin"
                    };

                    foreach (var possiblePluginName in possiblePluginNames)
                    {
                        try
                        {
                            targetFunction = kernel.Plugins.GetFunction(possiblePluginName, functionName);
                            _logger.LogInformation("✅ Found function '{FunctionName}' in plugin '{PluginName}'",
                                functionName, possiblePluginName);
                            break;
                        }
                        catch
                        {
                            // Try next variation
                        }
                    }

                    if (targetFunction == null)
                    {
                        var availablePlugins = string.Join(", ", kernel.Plugins.Select(p => p.Name));
                        return $"Error: Function '{functionName}' not found in any plugin. Available plugins: {availablePlugins}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to find function '{FunctionName}' in plugin '{PluginName}'",
                        functionName, pluginName);
                    return $"Error: Function '{functionName}' not found in plugin '{pluginName}': {ex.Message}";
                }

                // Convert JSON arguments to KernelArguments
                var kernelArgs = new KernelArguments();
                foreach (var parameter in targetFunction.Metadata.Parameters)
                {
                    if (arguments.TryGetProperty(parameter.Name, out var argValue))
                    {
                        var convertedValue = ConvertJsonValueToType(argValue, MapParameterType(parameter.ParameterType));
                        kernelArgs[parameter.Name] = convertedValue;
                        _logger.LogDebug("🔧 Parameter '{ParamName}': {Value}", parameter.Name, convertedValue);
                    }
                    else if (parameter.IsRequired)
                    {
                        return $"Error: Required parameter '{parameter.Name}' is missing for tool '{toolName}'";
                    }
                }

                // Invoke the function
                var result = await kernel.InvokeAsync(targetFunction, kernelArgs);
                var resultValue = result.GetValue<string>() ?? "Tool executed successfully";

                _logger.LogInformation("✅ Tool '{ToolName}' executed successfully", toolName);
                return $"Tool '{toolName}' executed successfully: {resultValue}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error executing individual tool '{ToolName}'", toolName);
                return $"Error executing tool '{toolName}': {ex.Message}";
            }
        }

        /// <summary>
        /// Generates tools dynamically from Semantic Kernel plugins (fallback method)
        /// </summary>
        private Task<List<object>> GenerateToolsFromKernelAsync()
        {
            var toolsList = new List<object>();

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();

                _logger.LogInformation("🔧 Generating tools dynamically from {PluginCount} plugins", kernel.Plugins.Count);

                foreach (var plugin in kernel.Plugins)
                {
                    foreach (var function in plugin)
                    {
                        var toolName = $"{plugin.Name}_{function.Name}".ToLowerInvariant();

                        var parameters = function.Metadata.Parameters.ToDictionary(
                            p => p.Name,
                            p => new
                            {
                                type = MapParameterType(p.ParameterType),
                                description = p.Description ?? "",
                                required = p.IsRequired
                            }
                        );

                        var requiredParams = function.Metadata.Parameters
                            .Where(p => p.IsRequired)
                            .Select(p => p.Name)
                            .ToArray();

                        toolsList.Add(new
                        {
                            name = toolName,
                            description = function.Description ?? $"Execute {function.Name} from {plugin.Name}",
                            inputSchema = new
                            {
                                type = "object",
                                properties = parameters,
                                required = requiredParams
                            }
                        });
                    }
                }

                _logger.LogInformation("✅ Generated {Count} tools dynamically from Kernel", toolsList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate tools from Kernel");
            }

            return Task.FromResult(toolsList);
        }

        /// <summary>
        /// Maps .NET parameter types to JSON schema types
        /// </summary>
        private static string MapParameterType(Type? parameterType)
        {
            if (parameterType == null) return "string";

            return parameterType.Name.ToLowerInvariant() switch
            {
                "string" => "string",
                "int32" or "int64" or "int16" => "integer",
                "double" or "float" or "decimal" => "number",
                "boolean" => "boolean",
                "datetime" => "string",
                _ => "string"
            };
        }

        /// <summary>
        /// Converts JSON value to appropriate .NET type
        /// </summary>
        private object? ConvertJsonValueToType(JsonElement jsonValue, string targetType)
        {
            return targetType.ToLowerInvariant() switch
            {
                "string" => jsonValue.GetString(),
                "integer" => jsonValue.GetInt32(),
                "number" => jsonValue.GetDouble(),
                "boolean" => jsonValue.GetBoolean(),
                _ => jsonValue.GetString()
            };
        }

        /// <summary>
        /// Handles streaming chat completions for MCP clients
        /// </summary>
        private Task<string> HandleStreamingChatAsync(string id, JsonElement @params, string sessionId)
        {
            try
            {
                // For streaming, return a response indicating streaming is not yet fully implemented
                // In a real implementation, this would use Server-Sent Events or WebSockets
                var streamingResponse = new
                {
                    id = id,
                    result = new
                    {
                        choices = new[]
                        {
                            new
                            {
                                index = 0,
                                delta = new
                                {
                                    role = "assistant",
                                    content = "Streaming response: This is a placeholder for streaming implementation. Full streaming support coming soon."
                                },
                                finish_reason = "stop"
                            }
                        },
                        usage = new
                        {
                            prompt_tokens = 100,
                            completion_tokens = 20,
                            total_tokens = 120
                        },
                        stream = true
                    }
                };

                return Task.FromResult(JsonSerializer.Serialize(streamingResponse));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming chat completion");
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    id = id,
                    error = new { code = -32603, message = $"Internal error: {ex.Message}" }
                }));
            }
        }
    }
}
