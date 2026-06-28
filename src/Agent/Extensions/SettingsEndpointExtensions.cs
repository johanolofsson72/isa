using Microsoft.Extensions.Options;
using Agent.Services;

namespace Agent.Extensions;

public static class SettingsEndpointExtensions
{
    public static WebApplication MapSettingsEndpoints(this WebApplication app)
    {
        // GET: Get system prompt (modular)
        app.MapGet(
                "/api/settings/systemprompt",
                async (ModularSystemPromptAssembler assembler, ILogger<object> logger) =>
                {
                    try
                    {
                        var content = await assembler.AssembleSystemPromptAsync();
                        return Results.Ok(content);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error assembling modular system prompt");
                        return Results.Problem(
                            $"An error occurred while assembling system prompt: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetSystemPrompt")
            .WithTags("Settings")
            .WithOpenApi();

        // GET: Get static system prompt (for editing)
        app.MapGet(
                "/api/settings/systemprompt/static",
                async (ModularSystemPromptAssembler assembler, ILogger<object> logger) =>
                {
                    try
                    {
                        var (coreInstructions, _, _) = await assembler.GetComponentsAsync();
                        return Results.Ok(coreInstructions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error reading static system prompt");
                        return Results.Problem(
                            $"An error occurred while reading static system prompt: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetStaticSystemPrompt")
            .WithTags("Settings")
            .WithOpenApi();

        // GET: Get users.json
        app.MapGet(
                "/api/settings/users",
                async (ModularSystemPromptAssembler assembler, ILogger<object> logger) =>
                {
                    try
                    {
                        var (_, users, _) = await assembler.GetComponentsAsync();
                        return Results.Ok(users);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error reading users.json");
                        return Results.Problem(
                            $"An error occurred while reading users.json: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetUsers")
            .WithTags("Settings")
            .WithOpenApi();

        // GET: Get plugins.json
        app.MapGet(
                "/api/settings/plugins",
                async (ModularSystemPromptAssembler assembler, ILogger<object> logger) =>
                {
                    try
                    {
                        var (_, _, plugins) = await assembler.GetComponentsAsync();
                        return Results.Ok(plugins);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error reading plugins.json");
                        return Results.Problem(
                            $"An error occurred while reading plugins.json: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetPlugins")
            .WithTags("Settings")
            .WithOpenApi();

        // GET: Get system prompt statistics
        app.MapGet(
                "/api/settings/systemprompt/stats",
                async (ModularSystemPromptAssembler assembler, ILogger<object> logger) =>
                {
                    try
                    {
                        var stats = await assembler.GetStatsAsync();
                        return Results.Ok(stats);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting system prompt stats");
                        return Results.Problem(
                            $"An error occurred while getting system prompt stats: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetSystemPromptStats")
            .WithTags("Settings")
            .WithOpenApi();



        // GET: Validate all components
        app.MapGet(
                "/api/settings/validate",
                async (ModularSystemPromptAssembler assembler, ILogger<object> logger) =>
                {
                    try
                    {
                        var validation = await assembler.ValidateComponentsAsync();
                        return Results.Ok(validation);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error validating components");
                        return Results.Problem(
                            $"An error occurred while validating components: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("ValidateComponents")
            .WithTags("Settings")
            .WithOpenApi();

        // GET: Get MCP capabilities (for MCP clients)
        app.MapGet(
                "/api/mcp/capabilities",
                async (DynamicMcpCapabilitiesGenerator generator, ILogger<object> logger) =>
                {
                    try
                    {
                        var capabilities = await generator.GenerateServerCapabilitiesAsync();
                        return Results.Ok(capabilities);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error generating MCP capabilities");
                        return Results.Problem(
                            $"An error occurred while generating MCP capabilities: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetMcpCapabilities")
            .WithTags("MCP")
            .WithOpenApi();

        // GET: Get MCP tools list (for MCP clients)
        app.MapGet(
                "/api/mcp/tools",
                async (DynamicMcpCapabilitiesGenerator generator, ILogger<object> logger) =>
                {
                    try
                    {
                        var tools = await generator.GenerateToolsListAsync();
                        return Results.Ok(tools);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error generating MCP tools list");
                        return Results.Problem(
                            $"An error occurred while generating MCP tools list: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetMcpTools")
            .WithTags("MCP")
            .WithOpenApi();

        // GET: Get MCP statistics
        app.MapGet(
                "/api/mcp/stats",
                async (DynamicMcpCapabilitiesGenerator generator, ILogger<object> logger) =>
                {
                    try
                    {
                        var stats = await generator.GetStatsAsync();
                        return Results.Ok(stats);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting MCP stats");
                        return Results.Problem(
                            $"An error occurred while getting MCP stats: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetMcpStats")
            .WithTags("MCP")
            .WithOpenApi();

        // PUT: Update system prompt
        app.MapPut(
                "/api/settings/systemprompt",
                async (
                    HttpRequest request,
                    IConfiguration configuration,
                    ILogger<object> logger
                ) =>
                {
                    try
                    {
                        var systemPromptFile =
                            configuration["LLM:SystemPromptFile"] ?? "systemprompt.txt";
                        var systemPromptPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            systemPromptFile
                        );

                        using var reader = new StreamReader(request.Body);
                        var content = await reader.ReadToEndAsync();

                        await File.WriteAllTextAsync(systemPromptPath, content);

                        logger.LogInformation("System prompt updated successfully");
                        return Results.Ok(new { message = "System prompt updated successfully" });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating system prompt");
                        return Results.Problem(
                            $"An error occurred while updating system prompt: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("UpdateSystemPrompt")
            .WithTags("Settings")
            .WithOpenApi();

        // GET: Get app settings
        app.MapGet(
                "/api/settings/appsettings",
                (ILogger<object> logger) =>
                {
                    try
                    {
                        var appSettingsPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "appsettings.json"
                        );

                        if (File.Exists(appSettingsPath))
                        {
                            var content = File.ReadAllText(appSettingsPath);
                            return Results.Ok(content);
                        }
                        else
                        {
                            logger.LogWarning(
                                "App settings file not found at {Path}",
                                appSettingsPath
                            );
                            return Results.NotFound("App settings file not found");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error reading app settings");
                        return Results.Problem(
                            $"An error occurred while reading app settings: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetAppSettings")
            .WithTags("Settings")
            .WithOpenApi();

        // PUT: Update app settings
        app.MapPut(
                "/api/settings/appsettings",
                async (HttpRequest request, ILogger<object> logger) =>
                {
                    try
                    {
                        var appSettingsPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "appsettings.json"
                        );

                        using var reader = new StreamReader(request.Body);
                        var content = await reader.ReadToEndAsync();

                        // Validate JSON format
                        try
                        {
                            System.Text.Json.JsonDocument.Parse(content);
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            return Results.BadRequest(new { error = "Invalid JSON format" });
                        }

                        await File.WriteAllTextAsync(appSettingsPath, content);

                        logger.LogInformation("App settings updated successfully");
                        return Results.Ok(new { message = "App settings updated successfully" });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating app settings");
                        return Results.Problem(
                            $"An error occurred while updating app settings: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("UpdateAppSettings")
            .WithTags("Settings")
            .WithOpenApi();

        return app;
    }
}
