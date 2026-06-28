using Agent.Services;
using Agent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

namespace Agent.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        // Use Swagger/OpenAPI services compatible with .NET 8.0
        services.AddEndpointsApiExplorer();
        // Simplified Swagger configuration for Docker compatibility
        services.AddSwaggerGen(c => { });
        
        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(
                "AllowAll",
                policy =>
                {
                    // SignalR requires specific origins instead of AllowAnyOrigin() for WebSockets
                    policy.WithOrigins(
                        "https://localhost:7041", // WebChat HTTPS
                        "http://localhost:5041",  // WebChat HTTP
                        "https://localhost:7136", // Fallback URL
                        "http://localhost:5020"   // Another fallback
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // Required for SignalR
                }
            );
        });

        return services;
    }
}

public static class WebApplicationExtensions
{
    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        // Use Swagger configuration compatible with .NET 8.0
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agent API v1");
                c.RoutePrefix = "swagger"; // Swagger UI will be available at /swagger
            });
        }
        
        return app;
    }

    public static WebApplication MapWorkflowEndpoints(this WebApplication app)
    {
        // Add workflow endpoints
        app.MapGet(
                "/workflows",
                async ([FromServices] WorkflowService workflowService) =>
                {
                    var workflows = await workflowService.LoadWorkflowsAsync();
                    return Results.Ok(
                        workflows.Select(w => new
                        {
                            w.Id,
                            w.Name,
                            w.Description,
                            w.Enabled,
                            w.ExecutionType,
                            JobCount = w.Jobs.Count
                        })
                    );
                }
            )
            .WithName("GetWorkflows")
            .WithSummary("Get all workflows")
            .WithDescription("Retrieves a list of all available workflows with their basic information")
            ;

        app.MapPost(
                "/workflows/{workflowId}/execute",
                async (string workflowId, [FromServices] WorkflowExecutor executor) =>
                {
                    try
                    {
                        var result = await executor.ExecuteWorkflowAsync(workflowId);
                        return Results.Ok(result);
                    }
                    catch (Exception ex)
                    {
                        return Results.BadRequest(new { error = ex.Message });
                    }
                }
            )
            .WithName("ExecuteWorkflow")
            .WithSummary("Execute a workflow")
            .WithDescription("Executes a specific workflow by its ID and returns the execution result")
            ;

        return app;
    }

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Aggregate health check (all registered checks) — keeps existing behavior on /health
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    timestamp = System.DateTime.UtcNow,
                    version = "1.0.0",
                    environment = app.Environment.EnvironmentName,
                    checks = report.Entries.Select(x => new
                    {
                        name = x.Key,
                        status = x.Value.Status.ToString(),
                        description = x.Value.Description,
                        data = x.Value.Data,
                        duration = x.Value.Duration.TotalMilliseconds
                    }),
                    totalDuration = report.TotalDuration.TotalMilliseconds
                };
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));
            }
        });

        // Liveness — process is alive. Skip all checks; returns 200 unless the host is dead.
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false,
        });

        // Readiness — can the LB send us new requests? Honors the readiness gate (drain flips to 503).
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        // Database — every DbContext can SELECT 1. The dependency-aware probe Swarm should hit.
        app.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("db"),
        });

        // Legacy simple health endpoint for backward compatibility
        app.MapGet(
                "/health/simple",
                () =>
                    new
                    {
                        status = "healthy",
                        timestamp = System.DateTime.UtcNow,
                        version = "1.0.0",
                        environment = app.Environment.EnvironmentName
                    }
            )
            .WithName("GetSimpleHealth")
            .WithSummary("Simple health check")
            .WithDescription("Returns a simple health status of the Agent service")
            ;

        // Agent info endpoint
        app.MapGet(
                "/info",
                async ([FromServices] WorkflowService workflowService) =>
                {
                    var workflows = await workflowService.LoadWorkflowsAsync();
                    return new
                    {
                        serviceName = "Agent",
                        version = "1.0.0",
                        environment = app.Environment.EnvironmentName,
                        timestamp = System.DateTime.UtcNow,
                        workflows = new
                        {
                            total = workflows.Count,
                            enabled = workflows.Count(w => w.Enabled),
                            scheduled = workflows.Count(w =>
                                w.Enabled && w.ExecutionType.ToLower() != "manual"
                            )
                        },
                        endpoints = new[]
                        {
                            "/health",
                            "/info",
                            "/workflows",
                            "/workflows/{id}/execute",
                            "/chathub (SignalR)",
                            "/swagger"
                        }
                    };
                }
            )
            .WithName("GetInfo")
            .WithSummary("Agent information")
            .WithDescription("Returns information about the Agent service and available workflows")
            ;

        return app;
    }
}
