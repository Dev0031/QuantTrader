using QuantTrader.Common.Models;

namespace QuantTrader.DataIngestion.Providers;

/// <summary>
/// Streaming abstraction for market data sources.
/// Implementations include Binance WebSocket (live), REST polling (fallback), and Fake (test/simulation).
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>Human-readable name of this provider (e.g., "BinanceWebSocket").</summary>
    string Name { get; }

    /// <summary>
    /// Streams market ticks for the given symbols, calling <paramref name="onTick"/> for each one.
    /// Runs until <paramref name="ct"/> is cancelled.
    /// </summary>
    Task StreamAsync(
        IReadOnlyList<string> symbols,
        Func<MarketTick, CancellationToken, Task> onTick,
        CancellationToken ct);
}
