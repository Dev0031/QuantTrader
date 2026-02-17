using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Models;
using QuantTrader.StrategyEngine.Strategies;

namespace QuantTrader.StrategyEngine.Services;

/// <summary>
/// Manages all registered strategies, maintains per-symbol candle buffers,
/// evaluates enabled strategies for each tick, applies confluence scoring,
/// and filters signals below the minimum confidence threshold.
/// </summary>
public sealed class StrategyManager : IStrategyManager
{
    private const int MaxCandleBuffer = 100;

    private readonly ILogger<StrategyManager> _logger;
    private readonly IEnumerable<IStrategy> _strategies;
    private readonly StrategySettings _strategySettings;

    /// <summary>Per-symbol circular candle buffer (last N candles).</summary>
    private readonly ConcurrentDictionary<string, LinkedList<Candle>> _candleBuffers = new();

    public StrategyManager(
        ILogger<StrategyManager> logger,
        IEnumerable<IStrategy> strategies,
        IOptions<StrategySettings> strategySettings)
    {
        _logger = logger;
        _strategies = strategies;
        _strategySettings = strategySettings.Value;
    }

    public Task<IReadOnlyList<TradeSignal>> EvaluateAsync(MarketTick tick, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var candles = GetCandles(tick.Symbol);

        if (candles.Count == 0)
        {
            _logger.LogDebug("No candles buffered for {Symbol}, skipping evaluation", tick.Symbol);
            return Task.FromResult<IReadOnlyList<TradeSignal>>(Array.Empty<TradeSignal>());
        }

        var readOnlyCandles = candles.ToList().AsReadOnly();
        var signals = new List<TradeSignal>();

        foreach (var strategy in _strategies)
        {
            if (!strategy.IsEnabled)
                continue;

            try
            {
                var signal = strategy.Evaluate(tick, readOnlyCandles);
                if (signal is not null)
                {
                    signals.Add(signal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Strategy {Strategy} threw an exception evaluating {Symbol}",
                    strategy.Name, tick.Symbol);
            }
        }

        if (signals.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<TradeSignal>>(Array.Empty<TradeSignal>());
        }

        // Apply confluence scoring: boost confidence when multiple strategies agree on direction
        var scoredSignals = ApplyConfluenceScoring(signals);

        // Filter below minimum confidence
        var filtered = scoredSignals
            .Where(s => s.ConfidenceScore >= _strategySettings.MinConfidenceScore)
            .ToList();

        if (filtered.Count < scoredSignals.Count)
        {
            _logger.LogDebug(
                "Filtered {Removed} signal(s) for {Symbol} below MinConfidenceScore {Min}",
                scoredSignals.Count - filtered.Count, tick.Symbol, _strategySettings.MinConfidenceScore);
        }

        _logger.LogInformation(
            "Evaluated {Total} strategies for {Symbol}: {SignalCount} signals generated, {FilteredCount} passed confidence filter",
            _strategies.Count(s => s.IsEnabled), tick.Symbol, signals.Count, filtered.Count);

        return Task.FromResult<IReadOnlyList<TradeSignal>>(filtered.AsReadOnly());
    }

    public void AppendCandle(Candle candle)
    {
        var buffer = _candleBuffers.GetOrAdd(candle.Symbol, _ => new LinkedList<Candle>());

        lock (buffer)
        {
            buffer.AddLast(candle);

            while (buffer.Count > MaxCandleBuffer)
            {
                buffer.RemoveFirst();
            }
        }

        _logger.LogDebug("Appended candle for {Symbol} [{Interval}]. Buffer size: {Size}",
            candle.Symbol, candle.Interval, buffer.Count);
    }

    public void ResetAll()
    {
        _candleBuffers.Clear();

        foreach (var strategy in _strategies)
        {
            try
            {
                strategy.Reset();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting strategy {Strategy}", strategy.Name);
            }
        }

        _logger.LogInformation("All strategies and candle buffers have been reset");
    }

    private LinkedList<Candle> GetCandles(string symbol)
    {
        return _candleBuffers.GetOrAdd(symbol, _ => new LinkedList<Candle>());
    }

    /// <summary>
    /// When multiple strategies agree on the same trade action for a symbol,
    /// their confidence scores are boosted proportionally to the number of agreeing strategies.
    /// </summary>
    private List<TradeSignal> ApplyConfluenceScoring(List<TradeSignal> signals)
    {
        if (signals.Count <= 1)
            return signals;

        // Group by action direction
        var buySignals = signals.Where(s => s.Action is Common.Enums.TradeAction.Buy).ToList();
        var sellSignals = signals.Where(s => s.Action is Common.Enums.TradeAction.Sell).ToList();

        var result = new List<TradeSignal>(signals.Count);

        result.AddRange(BoostConfluence(buySignals, signals.Count));
        result.AddRange(BoostConfluence(sellSignals, signals.Count));

        // Include non-buy/sell signals without boost
        result.AddRange(signals.Where(s =>
            s.Action is not Common.Enums.TradeAction.Buy and not Common.Enums.TradeAction.Sell));

        return result;
    }

    private static List<TradeSignal> BoostConfluence(List<TradeSignal> directionalSignals, int totalSignals)
    {
        if (directionalSignals.Count <= 1)
            return directionalSignals;

        // Confluence ratio: what fraction of strategies agree on this direction
        double confluenceBoost = (double)directionalSignals.Count / totalSignals * 0.3;

        return directionalSignals.Select(s =>
        {
            double boosted = Math.Min(1.0, s.ConfidenceScore + confluenceBoost);
            return s with { ConfidenceScore = boosted };
        }).ToList();
    }
}
