namespace Agent.Services.Resilience;

public interface IReadinessGate
{
    bool IsReady { get; }
    void MarkReady();
    void MarkUnready();
}

/// <summary>
/// Process-wide readiness flag. Flipped to false by GracefulDrain so the load balancer
/// drains traffic before in-flight requests finish and the WAL is checkpointed.
/// </summary>
public sealed class ReadinessGate : IReadinessGate
{
    private volatile bool _isReady = true;

    public bool IsReady => _isReady;

    public void MarkReady() => _isReady = true;
    public void MarkUnready() => _isReady = false;
}
