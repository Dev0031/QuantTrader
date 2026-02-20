using QuantTrader.Common.Models;

namespace QuantTrader.ApiGateway.Services;

/// <summary>
/// Holds the last-known-good values for portfolio and tick data.
/// Used by controllers to serve stale-but-valid data when Redis is temporarily unavailable.
/// Controllers should add the header <c>X-Data-Stale: true</c> when returning from this service.
/// Updated by <see cref="QuantTrader.ApiGateway.Workers.PortfolioSyncWorker"/> on each successful cycle.
/// </summary>
public sealed class StaleDataFallbackService
{
    private PortfolioSnapshot? _lastPortfolio;
    private readonly Dictionary<string, MarketTick> _lastTicks = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastUpdatedAt;
    private readonly object _lock = new();

    /// <summary>True if we have any fallback data at all.</summary>
    public bool HasData => _lastPortfolio is not null;

    /// <summary>When the fallback data was last successfully refreshed.</summary>
    public DateTimeOffset? LastUpdatedAt { get { lock (_lock) return _lastUpdatedAt; } }

    /// <summary>Returns the last successfully read portfolio snapshot, or null if never populated.</summary>
    public PortfolioSnapshot? GetPortfolioSnapshot() { lock (_lock) return _lastPortfolio; }

    /// <summary>Returns the last successfully read tick for a symbol, or null.</summary>
    public MarketTick? GetLatestTick(string symbol)
    {
        lock (_lock)
        {
            return _lastTicks.GetValueOrDefault(symbol.ToUpperInvariant());
        }
    }

    /// <summary>Called by PortfolioSyncWorker after each successful Redis read.</summary>
    public void UpdatePortfolio(PortfolioSnapshot snapshot)
    {
        lock (_lock)
        {
            _lastPortfolio = snapshot;
            _lastUpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Called by PortfolioSyncWorker after reading each tick price.</summary>
    public void UpdateTick(string symbol, MarketTick tick)
    {
        lock (_lock)
        {
            _lastTicks[symbol.ToUpperInvariant()] = tick;
            _lastUpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
