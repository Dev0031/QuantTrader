namespace QuantTrader.Infrastructure.Resilience;

/// <summary>
/// Singleton tracking the open/closed state of each circuit breaker.
/// Updated by Polly's OnOpened/OnClosed callbacks.
/// Read by health checks and service degradation logic.
/// </summary>
public sealed class CircuitBreakerState
{
    private volatile bool _isBinanceOpen;
    private volatile bool _isRedisOpen;
    private volatile bool _isEventBusOpen;

    /// <summary>True when the Binance order API circuit is open (calls failing).</summary>
    public bool IsBinanceOpen => _isBinanceOpen;

    /// <summary>True when the Redis circuit is open (cache unavailable).</summary>
    public bool IsRedisOpen => _isRedisOpen;

    /// <summary>True when the event bus circuit is open (messaging unavailable).</summary>
    public bool IsEventBusOpen => _isEventBusOpen;

    /// <summary>True if any circuit is currently open.</summary>
    public bool AnyOpen => _isBinanceOpen || _isRedisOpen || _isEventBusOpen;

    public void SetBinanceOpen(bool isOpen) => _isBinanceOpen = isOpen;
    public void SetRedisOpen(bool isOpen) => _isRedisOpen = isOpen;
    public void SetEventBusOpen(bool isOpen) => _isEventBusOpen = isOpen;
}
