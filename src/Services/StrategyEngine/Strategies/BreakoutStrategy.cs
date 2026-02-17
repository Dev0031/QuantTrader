using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Indicators.Volatility;
using QuantTrader.StrategyEngine.Configuration;

namespace QuantTrader.StrategyEngine.Strategies;

/// <summary>
/// Volatility Breakout strategy.
/// Tracks an N-period high/low range and generates signals when price
/// breaks above the range high (with volume confirmation) or below the range low.
/// Uses ATR for stop-loss and take-profit placement.
/// </summary>
public sealed class BreakoutStrategy : IStrategy
{
    private readonly ILogger<BreakoutStrategy> _logger;
    private readonly BreakoutSettings _settings;
    private readonly StrategySettings _strategySettings;

    private readonly AverageTrueRange _atr;

    public BreakoutStrategy(
        ILogger<BreakoutStrategy> logger,
        IOptions<BreakoutSettings> settings,
        IOptions<StrategySettings> strategySettings)
    {
        _logger = logger;
        _settings = settings.Value;
        _strategySettings = strategySettings.Value;

        _atr = new AverageTrueRange(_settings.AtrPeriod);
    }

    public string Name => "Breakout";

    public bool IsEnabled =>
        _strategySettings.EnabledStrategies.Contains(Name, StringComparer.OrdinalIgnoreCase);

    public TradeSignal? Evaluate(MarketTick tick, IReadOnlyList<Candle> recentCandles)
    {
        int rangePeriod = _settings.RangePeriod;

        // Need at least rangePeriod + 1 candles: rangePeriod for range, +1 for the current bar
        if (recentCandles.Count < rangePeriod + 1)
            return null;

        // Rebuild ATR from all candles
        _atr.Reset();
        foreach (var candle in recentCandles)
        {
            _atr.Update(candle.High, candle.Low, candle.Close);
        }

        if (!_atr.IsReady)
        {
            _logger.LogDebug("{Strategy}: ATR not ready for {Symbol}", Name, tick.Symbol);
            return null;
        }

        // Compute N-period range from the candles preceding the current (last) candle
        int rangeStart = recentCandles.Count - 1 - rangePeriod;
        int rangeEnd = recentCandles.Count - 1; // exclusive: the range excludes the latest candle
        decimal rangeHigh = decimal.MinValue;
        decimal rangeLow = decimal.MaxValue;

        for (int i = rangeStart; i < rangeEnd; i++)
        {
            if (recentCandles[i].High > rangeHigh)
                rangeHigh = recentCandles[i].High;
            if (recentCandles[i].Low < rangeLow)
                rangeLow = recentCandles[i].Low;
        }

        // Compute average volume over range period for volume confirmation
        decimal volumeSum = 0m;
        for (int i = rangeStart; i < rangeEnd; i++)
        {
            volumeSum += recentCandles[i].Volume;
        }
        decimal avgVolume = volumeSum / rangePeriod;

        decimal currentPrice = tick.Price;
        decimal currentVolume = tick.Volume;
        decimal atrValue = _atr.Value!.Value;

        TradeAction? action = null;

        // Bullish breakout: price above range high with volume confirmation
        if (currentPrice > rangeHigh && currentVolume > avgVolume * _settings.VolumeMultiplier)
        {
            action = TradeAction.Buy;
        }
        // Bearish breakout: price below range low (no volume requirement for sells)
        else if (currentPrice < rangeLow)
        {
            action = TradeAction.Sell;
        }

        if (action is null)
            return null;

        decimal stopLoss = action == TradeAction.Buy
            ? currentPrice - (_settings.AtrStopMultiplier * atrValue)
            : currentPrice + (_settings.AtrStopMultiplier * atrValue);

        decimal takeProfit = action == TradeAction.Buy
            ? currentPrice + (_settings.AtrProfitMultiplier * atrValue)
            : currentPrice - (_settings.AtrProfitMultiplier * atrValue);

        var signal = new TradeSignal(
            Id: Guid.NewGuid(),
            Symbol: tick.Symbol,
            Action: action.Value,
            Quantity: 0m,
            Price: currentPrice,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            Strategy: Name,
            ConfidenceScore: 0.65,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"));

        _logger.LogInformation(
            "{Strategy} signal: {Action} {Symbol} @ {Price} | Range=[{RangeLow},{RangeHigh}] Vol={Vol}/{AvgVol} SL={StopLoss} TP={TakeProfit}",
            Name, signal.Action, signal.Symbol, currentPrice, rangeLow, rangeHigh,
            currentVolume, avgVolume, stopLoss, takeProfit);

        return signal;
    }

    public void Reset()
    {
        _atr.Reset();
    }
}
