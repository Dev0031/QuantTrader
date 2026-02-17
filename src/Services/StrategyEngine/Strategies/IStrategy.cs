using QuantTrader.Common.Models;

namespace QuantTrader.StrategyEngine.Strategies;

/// <summary>Contract for all trading strategy implementations.</summary>
public interface IStrategy
{
    /// <summary>Unique display name for this strategy.</summary>
    string Name { get; }

    /// <summary>Whether this strategy is currently enabled for evaluation.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates the current market tick against recent candle history
    /// and returns a trade signal if conditions are met.
    /// </summary>
    /// <param name="tick">The latest market tick.</param>
    /// <param name="recentCandles">Recent OHLCV candles in chronological order (oldest first).</param>
    /// <returns>A trade signal if the strategy conditions are met; otherwise null.</returns>
    TradeSignal? Evaluate(MarketTick tick, IReadOnlyList<Candle> recentCandles);

    /// <summary>Resets all internal indicator state for this strategy.</summary>
    void Reset();
}
