namespace Agent.Services;

public record IdempotentResponse(int StatusCode, string? ContentType, string Body);

public interface IIdempotencyStore
{
    Task<IdempotentResponse?> TryReplayAsync(string key, CancellationToken ct = default);
    Task SaveAsync(string key, IdempotentResponse response, TimeSpan ttl, CancellationToken ct = default);
}
