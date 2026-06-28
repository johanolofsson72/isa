using Agent.Data;
using Agent.Services;
using Microsoft.EntityFrameworkCore;

namespace Agent.Extensions;

/// <summary>
/// Extension methods for startup configuration and initialization
/// </summary>
public static class StartupConfigurationExtensions
{
    /// <summary>
    /// Display startup information and configuration
    /// </summary>
    public static void DisplayStartupInfo(this WebApplicationBuilder builder)
    {
        Console.WriteLine($"Agent Version: 2025-08-17 21:52:58");
        Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
        Console.WriteLine("ENV ASPNETCORE_ENVIRONMENT: " + Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
        Console.WriteLine("ENV ISA_CONFIG_PATH: " + Environment.GetEnvironmentVariable("ISA_CONFIG_PATH"));
        
        foreach (var kvp in builder.Configuration.AsEnumerable())
        {
            Console.WriteLine($"[CONFIG] {kvp.Key} = {kvp.Value}");
        }
    }

    /// <summary>
    /// Initialize database with migrations
    /// </summary>
    public static Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("🔧 Checking database and applying migrations...");
            
            // Apply any pending migrations (creates database if it doesn't exist)
            dbContext.Database.Migrate();
            
            logger.LogInformation("✅ Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to apply database migrations");
            throw; // Re-throw to prevent startup with broken database
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Initialize plugins.json generation
    /// </summary>
    public static async Task InitializePluginsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        
        try
        {
            var jsonGenerator = scope.ServiceProvider.GetRequiredService<JsonPluginGenerator>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("🔄 [STARTUP] Ensuring plugins.json is current...");

            if (jsonGenerator.ShouldRegeneratePluginsJson())
            {
                var success = await jsonGenerator.GeneratePluginsJsonAsync();
                if (success)
                {
                    logger.LogInformation("✅ [STARTUP] plugins.json generated successfully");
                }
                else
                {
                    logger.LogWarning("⚠️ [STARTUP] Failed to generate plugins.json");
                }
            }
            else
            {
                logger.LogInformation("✅ [STARTUP] plugins.json is current, no regeneration needed");
            }

            // Generate MCP tool mapping
            var toolMappingGenerator = scope.ServiceProvider.GetRequiredService<McpToolMappingGenerator>();
            if (toolMappingGenerator.ShouldRegenerateToolMapping())
            {
                var toolMappingSuccess = await toolMappingGenerator.GenerateToolMappingAsync();
                if (toolMappingSuccess)
                {
                    logger.LogInformation("✅ [STARTUP] mcp-tool-mapping.json generated successfully");
                }
                else
                {
                    logger.LogWarning("⚠️ [STARTUP] Failed to generate mcp-tool-mapping.json");
                }
            }
            else
            {
                logger.LogInformation("✅ [STARTUP] mcp-tool-mapping.json is current, no regeneration needed");
            }

            logger.LogInformation("✅ [STARTUP] MCP configured in hybrid mode (individual tools + execute_task)");

        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "❌ [STARTUP] Error ensuring plugins.json and tool mapping are current");
        }
    }

    /// <summary>
    /// Debug system prompt loading (for troubleshooting)
    /// </summary>
    public static void DebugSystemPromptLoading(this WebApplication app)
    {
        try
        {
            var configuration = app.Services.GetRequiredService<IConfiguration>();
            var systemPromptFile = configuration.GetValue<string>("LLM:SystemPromptFile") ?? "systemprompt.txt";

            Console.WriteLine($"🔍 [STARTUP DEBUG] SystemPromptFile configured as: {systemPromptFile}");

            if (File.Exists(systemPromptFile))
            {
                var content = File.ReadAllText(systemPromptFile);
                Console.WriteLine($"✅ [STARTUP DEBUG] SystemPrompt file found and loaded successfully");
                Console.WriteLine($"📄 [STARTUP DEBUG] File size: {content.Length} characters");
                Console.WriteLine($"📝 [STARTUP DEBUG] First 200 characters: {content.Substring(0, Math.Min(200, content.Length))}...");
            }
            else
            {
                Console.WriteLine($"❌ [STARTUP DEBUG] SystemPrompt file NOT FOUND at: {systemPromptFile}");
                Console.WriteLine($"📁 [STARTUP DEBUG] Current directory: {Directory.GetCurrentDirectory()}");
                Console.WriteLine($"📂 [STARTUP DEBUG] Files in current directory:");
                foreach (var file in Directory.GetFiles(".", "*.txt"))
                {
                    Console.WriteLine($"   - {file}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [STARTUP DEBUG] Error loading systemprompt: {ex.Message}");
        }
    }
}
