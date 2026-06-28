using System.Text;
using Agent.Services;

namespace Agent.Middleware;

/// <summary>
/// Caches the response of mutating requests (POST/PUT/PATCH/DELETE) keyed by the
/// Idempotency-Key header. Replays the cached response on duplicate requests so a
/// retry from the load balancer after a spot kill does not double-charge or double-write.
/// Pragmatic implementation: only caches 2xx responses.
/// </summary>
public class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store, IConfiguration configuration)
    {
        if (!MutatingMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues))
        {
            await next(context);
            return;
        }

        var key = keyValues.ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 64)
        {
            await next(context);
            return;
        }

        var replay = await store.TryReplayAsync(key, context.RequestAborted);
        if (replay is not null)
        {
            context.Response.StatusCode = replay.StatusCode;
            if (!string.IsNullOrEmpty(replay.ContentType))
            {
                context.Response.ContentType = replay.ContentType;
            }
            await context.Response.WriteAsync(replay.Body, context.RequestAborted);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();

            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody, context.RequestAborted);

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                var ttlHours = configuration.GetValue("Idempotency:TtlHours", 24);
                await store.SaveAsync(
                    key,
                    new IdempotentResponse(context.Response.StatusCode, context.Response.ContentType, body),
                    TimeSpan.FromHours(ttlHours),
                    context.RequestAborted);
            }
            else
            {
                logger.LogDebug("Skipping idempotency cache for non-2xx response (status {Status}, key {Key})", context.Response.StatusCode, key);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}
