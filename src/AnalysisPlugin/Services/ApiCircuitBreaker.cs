using Microsoft.Extensions.Logging;
using AnalysisPlugin.Configuration;

namespace AnalysisPlugin.Services;

/// <summary>
/// 🔌 Circuit Breaker för externa API:er - förhindrar kaskadfel
/// </summary>
public class ApiCircuitBreaker
{
    private readonly ILogger<ApiCircuitBreaker> _logger;
    private readonly GeneralSettings _settings;
    private readonly Dictionary<string, CircuitState> _circuits = new();
    private readonly object _lock = new();

    public ApiCircuitBreaker(ILogger<ApiCircuitBreaker> logger, GeneralSettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public async Task<T> ExecuteAsync<T>(string circuitName, Func<Task<T>> apiCall, Func<T> fallback)
    {
        var circuit = GetOrCreateCircuit(circuitName);

        lock (_lock)
        {
            if (circuit.State == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - circuit.LastFailureTime < TimeSpan.FromMinutes(_settings.CircuitBreakerTimeoutMinutes))
                {
                    _logger.LogWarning("🔴 Circuit breaker OPEN för {CircuitName} - använder fallback", circuitName);
                    return fallback();
                }
                else
                {
                    // Försök att stänga circuit breaker
                    circuit.State = CircuitBreakerState.HalfOpen;
                    _logger.LogInformation("🟡 Circuit breaker HALF-OPEN för {CircuitName} - testar anslutning", circuitName);
                }
            }
        }

        try
        {
            var result = await apiCall();
            
            lock (_lock)
            {
                // Framgång - återställ circuit breaker
                circuit.FailureCount = 0;
                circuit.State = CircuitBreakerState.Closed;
                if (circuit.LastFailureTime != DateTime.MinValue)
                {
                    _logger.LogInformation("🟢 Circuit breaker CLOSED för {CircuitName} - anslutning återställd", circuitName);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                circuit.FailureCount++;
                circuit.LastFailureTime = DateTime.UtcNow;

                if (circuit.FailureCount >= _settings.CircuitBreakerThreshold)
                {
                    circuit.State = CircuitBreakerState.Open;
                    _logger.LogError(ex, "🔴 Circuit breaker ÖPPNAD för {CircuitName} efter {FailureCount} fel", 
                        circuitName, circuit.FailureCount);
                }
                else
                {
                    _logger.LogWarning(ex, "⚠️ API-fel för {CircuitName} ({FailureCount}/{Threshold})", 
                        circuitName, circuit.FailureCount, _settings.CircuitBreakerThreshold);
                }
            }

            return fallback();
        }
    }

    private CircuitState GetOrCreateCircuit(string circuitName)
    {
        lock (_lock)
        {
            if (!_circuits.ContainsKey(circuitName))
            {
                _circuits[circuitName] = new CircuitState
                {
                    Name = circuitName,
                    State = CircuitBreakerState.Closed,
                    FailureCount = 0,
                    LastFailureTime = DateTime.MinValue
                };
            }
            return _circuits[circuitName];
        }
    }

    public Dictionary<string, CircuitBreakerStatus> GetStatus()
    {
        lock (_lock)
        {
            return _circuits.ToDictionary(
                kvp => kvp.Key,
                kvp => new CircuitBreakerStatus
                {
                    State = kvp.Value.State,
                    FailureCount = kvp.Value.FailureCount,
                    LastFailureTime = kvp.Value.LastFailureTime,
                    IsHealthy = kvp.Value.State == CircuitBreakerState.Closed
                }
            );
        }
    }
}

internal class CircuitState
{
    public string Name { get; set; } = "";
    public CircuitBreakerState State { get; set; }
    public int FailureCount { get; set; }
    public DateTime LastFailureTime { get; set; }
}

public enum CircuitBreakerState
{
    Closed,   // Normal drift
    Open,     // Blockerad på grund av fel
    HalfOpen  // Testar om tjänsten är tillgänglig igen
}

public class CircuitBreakerStatus
{
    public CircuitBreakerState State { get; set; }
    public int FailureCount { get; set; }
    public DateTime LastFailureTime { get; set; }
    public bool IsHealthy { get; set; }
}
