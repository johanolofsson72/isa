using Agent.BackgroundServices;
using Agent.Data;
using Agent.Endpoints;
using Agent.Extensions;
using Agent.Middleware;
using Agent.Services;
using Agent.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Quartz;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Spot-safe shutdown window: Kestrel finishes in-flight work, then SqliteCheckpointOnStop runs.
// Compose `stop_grace_period` must be at least Kestrel timeout + drain delay + checkpoint headroom.
builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(
    builder.Configuration.GetValue("Spot:KestrelShutdownTimeoutSeconds", 20)));

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine(
    "ENV ASPNETCORE_ENVIRONMENT: " + Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
);
Console.WriteLine("ENV ISA_CONFIG_PATH: " + Environment.GetEnvironmentVariable("ISA_CONFIG_PATH"));
foreach (var kvp in builder.Configuration.AsEnumerable())
{
    Console.WriteLine($"[CONFIG] {kvp.Key} = {kvp.Value}");
}

// Add services to the container
builder.Services.AddSwaggerDocumentation();
// SignalR removed - using REST API for chat instead
builder.Services.AddCorsPolicy();

// Add chat session service for REST API (Scoped to avoid DI issues)
builder.Services.AddScoped<IChatSessionService, ChatSessionService>();

// Add HttpClient for services that need it with SSL certificate handling for development
builder.Services.AddHttpClient()
    .ConfigureHttpClientDefaults(http =>
    {
        // Configure SSL certificate handling for development
        http.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var handler = new HttpClientHandler();

            // Only for development - accept all SSL certificates
            if (builder.Environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback = (
                    sender,
                    certificate,
                    chain,
                    sslPolicyErrors
                ) => true;

                var logger = serviceProvider.GetService<ILogger<Program>>();
                logger?.LogWarning("🔓 [Agent] SSL certificate validation disabled for development");
            }

            return handler;
        });
    });

// Add HttpContextAccessor for accessing HTTP context in services
builder.Services.AddHttpContextAccessor();

// Add controllers for Quartz admin API
builder.Services.AddControllers();

// Database
builder.Services.AddDatabaseServices(builder.Configuration);

// Core services
builder.Services.AddCacheAndAuditServices();
builder.Services.AddChatServices();
builder.Services.AddWorkflowServices();

// Plugins + MCP + Memory
builder.Services.AddPluginServices();
builder.Services.AddMcpServices(builder.Configuration);
builder.Services.AddMemoryServices();

// Quartz + monitoring
builder.Services.AddQuartzServices();

// Navigation service (WorkflowValidationService is already registered in AddWorkflowServices)
builder.Services.AddScoped<WorkflowNavigationService>();

// Chat execution service for consistent AI behavior between webchat and workflows
builder.Services.AddScoped<IChatExecutionService, ChatExecutionService>();

// File processing service for handling document interpretation
builder.Services.AddScoped<FileProcessingService>();

// Conversation history and user notes services
builder.Services.AddScoped<IConversationHistoryService, ConversationHistoryService>();
builder.Services.AddScoped<IUserNotesService, UserNotesService>();
builder.Services.AddScoped<SmartUserIdentificationService>();

// Schedule parser
builder.Services.AddSingleton<IWorkflowScheduleParser, WorkflowScheduleParser>();

// Background services
builder.Services.AddBackgroundServices(args);

// Semantic Kernel (LiteLLM-backed via OpenAI connector)
builder.Services.AddSemanticKernelServices(builder.Configuration);

// Spot resilience: IMDS watcher, graceful drain, WAL checkpoint, idempotency, healthchecks
builder.Services.AddSpotResilience();


// Note: Controllers removed in favor of minimal API endpoints

// Additional workflow services (core services already registered in AddWorkflowServices)
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<WorkflowMigrationService>();
builder.Services.AddScoped<WorkflowRollbackService>();

// Register main MCP service (other MCP services already registered in AddMcpServices)
builder.Services.AddSingleton<McpService>(sp => new McpService(
    sp,
    builder.Configuration,
    sp.GetRequiredService<DynamicMcpCapabilitiesGenerator>(),
    sp.GetRequiredService<McpClientDiscoveryService>(),
    sp.GetRequiredService<ILogger<McpService>>(),
    sp.GetRequiredService<IMcpStreamingService>(),
    sp.GetRequiredService<IMcpProgressService>(),
    sp.GetRequiredService<McpToolMappingGenerator>()));

var app = builder.Build();

// DEBUG: Verify systemprompt.txt is loaded correctly at startup
try
{
    var configuration = app.Services.GetRequiredService<IConfiguration>();
    var systemPromptFile =
        configuration.GetValue<string>("LLM:SystemPromptFile") ?? "systemprompt.txt";

    Console.WriteLine($"🔍 [STARTUP DEBUG] SystemPromptFile configured as: {systemPromptFile}");

    if (File.Exists(systemPromptFile))
    {
        var content = File.ReadAllText(systemPromptFile);
        Console.WriteLine($"✅ [STARTUP DEBUG] SystemPrompt file found and loaded successfully");
        Console.WriteLine($"📄 [STARTUP DEBUG] File size: {content.Length} characters");
        Console.WriteLine(
            $"📝 [STARTUP DEBUG] First 200 characters: {content.Substring(0, Math.Min(200, content.Length))}..."
        );
    }
    else
    {
        Console.WriteLine($"❌ [STARTUP DEBUG] SystemPrompt file NOT FOUND at: {systemPromptFile}");
        Console.WriteLine(
            $"📁 [STARTUP DEBUG] Current directory: {Directory.GetCurrentDirectory()}"
        );
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

// Ensure databases are created (simplified Docker-compatible approach)
try
{
    using var scope = app.Services.CreateScope();

    // Initialize main Agent database
    var agentDbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
    agentDbContext.Database.EnsureCreated();
    Console.WriteLine("✅ Agent database initialized successfully");

    // Initialize Memory database
    var memoryDbContext = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
    memoryDbContext.Database.EnsureCreated();
    Console.WriteLine("✅ Memory database initialized successfully");

    // Initialize Files database
    var filesDbContext = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
    filesDbContext.Database.Migrate();
    Console.WriteLine("✅ Files database initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
    // Continue startup - database will be created on first use
}

// Ensure plugins.json is generated at startup for MCP capabilities
using (var scope = app.Services.CreateScope())
{
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

        // MCP tool mapping removed - using single execute_task tool in autonomous agent mode
        logger.LogInformation("✅ [STARTUP] MCP configured for autonomous agent mode with execute_task tool");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ [STARTUP] Error ensuring plugins.json and tool mapping are current");
    }
}

// Configure the HTTP request pipeline
app.UseSwaggerDocumentation();
app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Idempotency-Key replay (must come BEFORE endpoints so it can wrap responses)
app.UseMiddleware<IdempotencyMiddleware>();

// Enable static files for Quartz admin interface
app.UseStaticFiles();

// Note: CrystalQuartz Web UI removed due to .NET 9 compatibility issues
// Quartz.NET monitoring is available through custom API endpoints

// SignalR hub removed - using REST API for chat instead

// Map all endpoints (using Minimal API pattern)
app.MapCacheAndAuditEndpoints();
app.MapWorkflowEndpoints(); // Basic workflow endpoints from WebApplicationExtensions
app.MapChatEndpoints(); // REST API chat endpoints
app.MapWorkflowSchedulingEndpoints(); // Workflow scheduling endpoints from WorkflowEndpointExtensions
app.MapMigrationEndpoints(); // Migration endpoints for Quartz.NET workflow migration
app.MapMonitoringEndpoints(); // Monitoring endpoints for job execution tracking
app.MapHistoryEndpoints(); // Add history endpoints
app.MapSettingsEndpoints(); // Add settings endpoints
app.MapPluginManagementEndpoints(); // Add plugin management endpoints - THIS IS THE CORE FUNCTIONALITY
app.MapMemoryEndpoints(); // Add memory system endpoints
app.MapUserIdentificationEndpoints(); // Add user identification endpoints
app.MapDocumentEndpoints(); // Add document processing endpoints
app.MapFileEndpoints(); // Add file upload and management endpoints
app.MapHealthEndpoints();
app.MapMcpEndpoints(); // MCP endpoints - THIS IS THE CORE FUNCTIONALITY

// Force kernel creation to trigger plugin discovery during startup
try
{
    using var scope = app.Services.CreateScope();
    var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();
    Console.WriteLine($"✅ Kernel created with {kernel.Plugins.Count} plugins registered");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Failed to create kernel during startup: {ex.Message}");
}

// Register job execution listeners with Quartz scheduler
try
{
    var schedulerFactory = app.Services.GetRequiredService<ISchedulerFactory>();
    var scheduler = await schedulerFactory.GetScheduler();

    // Register original job execution listener
    var jobListener = app.Services.GetRequiredService<JobExecutionListener>();
    scheduler.ListenerManager.AddJobListener(jobListener);

    // Register new workflow monitoring listener
    var workflowListener = app.Services.GetRequiredService<WorkflowJobListener>();
    scheduler.ListenerManager.AddJobListener(workflowListener);

    Console.WriteLine("✅ Job execution listeners registered with Quartz scheduler");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Failed to register job execution listeners: {ex.Message}");
}

Console.WriteLine("✅ Startup complete. All registered.");

app.Run();

// Make Program class available for testing
internal partial class Program { }
