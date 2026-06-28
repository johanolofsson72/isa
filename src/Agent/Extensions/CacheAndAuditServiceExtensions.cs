using Agent.Data;
using Agent.Services;

namespace Agent.Extensions;

/// <summary>
/// Extensions for registering cache and audit services
/// </summary>
public static class CacheAndAuditServiceExtensions
{
    /// <summary>
    /// Adds cache and audit services to the service collection
    /// Uses the unified AgentDbContext for all data access
    /// </summary>
    public static IServiceCollection AddCacheAndAuditServices(this IServiceCollection services)
    {
        // Register services - they will use the existing AgentDbContext
        services.AddScoped<IQueryCacheService, QueryCacheService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
        services.AddScoped<IOpenAICacheService, OpenAICacheService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
