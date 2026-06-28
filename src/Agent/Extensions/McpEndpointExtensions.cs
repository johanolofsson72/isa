using Agent.Services;

namespace Agent.Extensions;

/// <summary>
/// Extension methods for MCP (Model Context Protocol) endpoints
/// </summary>
public static class McpEndpointExtensions
{
    /// <summary>
    /// Map MCP HTTP endpoint for VSCode and other clients
    /// </summary>
    public static WebApplication MapMcpEndpoints(this WebApplication app)
    {
        app.MapPost("/mcp", async (HttpContext context, McpService mcpService) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var json = await reader.ReadToEndAsync();
            var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
            var origin = context.Request.Headers["Origin"].FirstOrDefault();

            var response = await mcpService.HandleMcpRequestAsync(json, sessionId, userAgent, origin);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(response);
        });

        return app;
    }
}
