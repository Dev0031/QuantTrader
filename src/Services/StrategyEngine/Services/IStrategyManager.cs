using QuantTrader.Common.Models;

namespace QuantTrader.StrategyEngine.Services;

/// <summary>Orchestrates strategy evaluation across all registered strategies.</summary>
public interface IStrategyManager
{
    /// <summary>
    /// Evaluates all enabled strategies for a given market tick.
    /// Returns trade signals that pass the minimum confidence threshold.
    /// </summary>
    Task<IReadOnlyList<TradeSignal>> EvaluateAsync(MarketTick tick, CancellationToken ct = default);

    /// <summary>
    /// Appends a completed candle to the per-symbol buffer.
    /// </summary>
    void AppendCandle(Candle candle);

    /// <summary>Resets all strategy state and candle buffers.</summary>
    void ResetAll();
}
