using Agent.BackgroundServices;
using Agent.Configuration;
using Agent.Data;
using Agent.Services;
using Agent.Services.Interfaces;
using Agent.Services.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Quartz;
using Shared.Services;

namespace Agent.Extensions;


/// <summary>
/// Extension methods for registering services in the DI container
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Add Entity Framework and database services with SQLite PRAGMA hardening
    /// </summary>
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SqlitePragmaInterceptor>();

        services.AddDbContext<AgentDbContext>((sp, options) =>
            options
                .UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=agent.db")
                .AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>())
        );

        services.AddDbContext<MemoryDbContext>((sp, options) =>
            options
                .UseSqlite(configuration.GetConnectionString("MemoryConnection") ?? "Data Source=memory.db")
                .AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>())
        );

        services.AddDbContext<FilesDbContext>((sp, options) =>
            options
                .UseSqlite(configuration.GetConnectionString("FilesConnection") ?? "Data Source=files.db")
                .AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>())
        );

        services.AddScoped<ITempFilesService, TempFilesService>();
        services.AddHostedService<TempFilesCleanupService>();

        return services;
    }

    /// <summary>
    /// Add memory and identity services
    /// </summary>
    public static IServiceCollection AddMemoryServices(this IServiceCollection services)
    {
        services.AddScoped<IProductionMemoryService, ProductionMemoryService>();
        services.AddScoped<IIntelligentMemoryExtractor, IntelligentMemoryExtractor>();

        services.AddScoped<Shared.Services.IDeviceFingerprintService, Shared.Services.DeviceFingerprintService>();
        services.AddHttpContextAccessor();

        services.AddScoped<IOnlineSearchService, OnlineSearchService>();

        services.AddHostedService<MemoryMaintenanceService>();

        return services;
    }

    /// <summary>
    /// Add plugin discovery and dynamic system prompt services
    /// </summary>
    public static IServiceCollection AddPluginServices(this IServiceCollection services)
    {
        services.AddSingleton<PluginMetadataExtractor>();
        services.AddSingleton<DynamicPluginDiscoveryService>();
        services.AddScoped<DatabaseAwarePluginDiscoveryService>();
        services.AddScoped<HybridPluginDiscoveryService>();
        services.AddScoped<DynamicPluginDiscoveryServiceWithFallback>();
        services.AddScoped<IDynamicSystemPromptGenerator, DynamicSystemPromptGenerator>();

        services.AddSingleton<JsonPluginGenerator>();
        services.AddSingleton<McpToolMappingGenerator>();
        services.AddSingleton<JsonSchemaValidator>();
        services.AddSingleton<ModularSystemPromptAssembler>();

        return services;
    }

    /// <summary>
    /// Add MCP (Model Context Protocol) services
    /// </summary>
    public static IServiceCollection AddMcpServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DynamicMcpCapabilitiesGenerator>();
        services.AddSingleton<McpClientDiscoveryService>();

        services.AddSingleton<IMcpProgressService, McpProgressService>();
        services.AddSingleton<IMcpStreamingService, McpStreamingService>();

        services.AddSingleton<McpService>(sp => new McpService(
            sp,
            configuration,
            sp.GetRequiredService<DynamicMcpCapabilitiesGenerator>(),
            sp.GetRequiredService<McpClientDiscoveryService>(),
            sp.GetRequiredService<ILogger<McpService>>(),
            sp.GetRequiredService<IMcpStreamingService>(),
            sp.GetRequiredService<IMcpProgressService>(),
            sp.GetRequiredService<McpToolMappingGenerator>()));

        return services;
    }

    /// <summary>
    /// Add background services
    /// </summary>
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, string[] args)
    {
        services.AddHostedService<DatabaseCleanupService>();
        services.AddHostedService<KeepAliveService>();
        services.AddHostedService<YamlWorkflowSyncService>();
        services.AddHostedService<DailyOnlineSearchService>();

        if (args.Contains("--mcp-server"))
        {
            Console.WriteLine("[MCP] MCP WebSocket-server enabled (port 5100)");
            services.AddHostedService<McpWebSocketServer>();
        }

        return services;
    }

    /// <summary>
    /// Add Semantic Kernel services + LiteLLM HTTP client + CloudflareAccess options
    /// </summary>
    public static IServiceCollection AddSemanticKernelServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LiteLLMOptions>(configuration.GetSection(LiteLLMOptions.SectionName));
        services.Configure<CloudflareAccessOptions>(configuration.GetSection(CloudflareAccessOptions.SectionName));

        var llmOptions = configuration.GetSection(LiteLLMOptions.SectionName).Get<LiteLLMOptions>() ?? new LiteLLMOptions();

        services.AddHttpClient(KernelExtensions.LiteLLMHttpClient, http =>
        {
            http.Timeout = TimeSpan.FromMinutes(llmOptions.TimeoutMinutes);
            // BaseAddress NOT set here — endpoint is supplied to OpenAIClientOptions.Endpoint instead.
        });

        services.AddSingleton<Kernel>(serviceProvider =>
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            return KernelExtensions.CreateKernelWithPlugins(configuration, loggerFactory, serviceProvider);
        });

        services.AddSingleton<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(serviceProvider =>
        {
            var kernel = serviceProvider.GetRequiredService<Kernel>();
            return kernel.GetRequiredService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>();
        });

        return services;
    }

    /// <summary>
    /// Add workflow and execution services
    /// </summary>
    public static IServiceCollection AddWorkflowServices(this IServiceCollection services)
    {
        services.AddSingleton<WorkflowService>();
        services.AddSingleton<HistoryService>();
        services.AddScoped<WorkflowExecutor>();
        services.AddScoped<IExecutionContextService, ExecutionContextService>();

        services.AddScoped<WorkflowValidationService>();
        services.AddScoped<IWorkflowValidationService>(provider => provider.GetRequiredService<WorkflowValidationService>());

        return services;
    }

    /// <summary>
    /// Add chat and messaging services
    /// </summary>
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddScoped<IMessageSanitizationService, MessageSanitizationService>();
        services.AddScoped<IChatHistoryFactory, ChatHistoryFactory>();

        return services;
    }


    /// <summary>
    /// Add Quartz and monitoring services
    /// </summary>
    public static IServiceCollection AddQuartzServices(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
            q.AddJob<WorkflowExecutionJob>(opts => opts.WithIdentity("workflow-job-template").StoreDurably(true));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        services.AddSingleton<IScheduler>(sp =>
        {
            var factory = sp.GetRequiredService<ISchedulerFactory>();
            return factory.GetScheduler().GetAwaiter().GetResult();
        });

        services.AddSingleton<QuartzJobRegistrationService>();
        services.AddScoped<JobExecutionTrackingService>();
        services.AddSingleton<JobExecutionListener>();
        services.AddSingleton<WorkflowMetricsCollector>();
        services.AddSingleton<NotificationRateLimitService>();
        services.AddSingleton<WorkflowNotificationService>();
        services.AddSingleton<WorkflowJobListener>();
        services.AddHostedService<DailySummaryService>();

        services.AddHealthChecks().AddCheck<QuartzHealthCheckService>("quartz-scheduler", tags: new[] { "scheduler", "workflows" });
        services.AddSingleton<QuartzHealthCheckService>();

        return services;
    }

    /// <summary>
    /// Spot-resilience: eviction watcher (IMDS), readiness gate, graceful drain,
    /// SQLite WAL checkpoint on shutdown, and idempotency-key replay store.
    /// Order matters: GracefulDrain MUST register BEFORE SqliteCheckpointOnStop so
    /// readiness flips first, drain delay elapses, then the checkpoint runs.
    /// </summary>
    public static IServiceCollection AddSpotResilience(this IServiceCollection services)
    {
        services.AddSingleton<IReadinessGate, ReadinessGate>();

        services.AddHttpClient("imds");
        services.AddHostedService<SpotEvictionWatcher>();

        services.AddHostedService<GracefulDrain>();
        services.AddHostedService<SqliteCheckpointOnStop>();

        services.AddScoped<IIdempotencyStore, SqliteIdempotencyStore>();

        services.AddHealthChecks()
            .AddCheck<ReadinessHealthCheck>("readiness", tags: new[] { "ready" })
            .AddDbContextCheck<AgentDbContext>("agent-db", tags: new[] { "db" })
            .AddDbContextCheck<MemoryDbContext>("memory-db", tags: new[] { "db" })
            .AddDbContextCheck<FilesDbContext>("files-db", tags: new[] { "db" });

        return services;
    }
}

/// <summary>
/// Health check that fails when the readiness gate is unready (drain in progress).
/// Tagged "ready" so /health/ready can filter on it.
/// </summary>
public sealed class ReadinessHealthCheck(IReadinessGate gate) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(gate.IsReady
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("ready")
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("draining"));
    }
}
