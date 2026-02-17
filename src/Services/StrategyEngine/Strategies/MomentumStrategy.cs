using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Indicators.Oscillators;
using QuantTrader.Indicators.Volatility;
using QuantTrader.StrategyEngine.Configuration;

namespace QuantTrader.StrategyEngine.Strategies;

/// <summary>
/// RSI + MACD Momentum strategy.
/// Buy when RSI is oversold AND MACD histogram turns positive.
/// Sell when RSI is overbought AND MACD histogram turns negative.
/// Stop-loss at recent swing low/high with ATR as fallback.
/// </summary>
public sealed class MomentumStrategy : IStrategy
{
    private readonly ILogger<MomentumStrategy> _logger;
    private readonly MomentumSettings _settings;
    private readonly StrategySettings _strategySettings;

    private readonly RelativeStrengthIndex _rsi;
    private readonly MACD _macd;
    private readonly AverageTrueRange _atr;

    public MomentumStrategy(
        ILogger<MomentumStrategy> logger,
        IOptions<MomentumSettings> settings,
        IOptions<StrategySettings> strategySettings)
    {
        _logger = logger;
        _settings = settings.Value;
        _strategySettings = strategySettings.Value;

        _rsi = new RelativeStrengthIndex(_settings.RsiPeriod);
        _macd = new MACD(_settings.MacdFastPeriod, _settings.MacdSlowPeriod, _settings.MacdSignalPeriod);
        _atr = new AverageTrueRange(_settings.AtrPeriod);
    }

    public string Name => "Momentum";

    public bool IsEnabled =>
        _strategySettings.EnabledStrategies.Contains(Name, StringComparer.OrdinalIgnoreCase);

    public TradeSignal? Evaluate(MarketTick tick, IReadOnlyList<Candle> recentCandles)
    {
        if (recentCandles.Count < 2)
            return null;

        // Rebuild indicators from candle history
        _rsi.Reset();
        _macd.Reset();
        _atr.Reset();

        decimal? previousHistogram = null;

        for (int i = 0; i < recentCandles.Count; i++)
        {
            var candle = recentCandles[i];

            _rsi.Update(candle.Close);
            _macd.Update(candle.Close);
            _atr.Update(candle.High, candle.Low, candle.Close);

            if (i == recentCandles.Count - 2)
            {
                previousHistogram = _macd.Histogram;
            }
        }

        if (!_rsi.IsReady || !_macd.IsReady || !_atr.IsReady)
        {
            _logger.LogDebug("{Strategy}: Indicators not ready for {Symbol}", Name, tick.Symbol);
            return null;
        }

        double rsiValue = _rsi.Value!.Value;
        decimal currentHistogram = _macd.Histogram!.Value;
        decimal atrValue = _atr.Value!.Value;

        if (!previousHistogram.HasValue)
            return null;

        TradeAction? action = null;

        // Buy: RSI oversold AND histogram turned positive
        bool histogramTurnedPositive = previousHistogram.Value <= 0m && currentHistogram > 0m;
        bool histogramTurnedNegative = previousHistogram.Value >= 0m && currentHistogram < 0m;

        if (rsiValue < _settings.RsiOversold && histogramTurnedPositive)
        {
            action = TradeAction.Buy;
        }
        else if (rsiValue > _settings.RsiOverbought && histogramTurnedNegative)
        {
            action = TradeAction.Sell;
        }

        if (action is null)
            return null;

        // Compute stop-loss from swing low/high with ATR fallback
        decimal stopLoss = ComputeSwingStop(action.Value, tick.Price, recentCandles, atrValue);

        // Take-profit mirrors the risk distance
        decimal riskDistance = Math.Abs(tick.Price - stopLoss);
        decimal takeProfit = action == TradeAction.Buy
            ? tick.Price + (riskDistance * 1.5m)
            : tick.Price - (riskDistance * 1.5m);

        var signal = new TradeSignal(
            Id: Guid.NewGuid(),
            Symbol: tick.Symbol,
            Action: action.Value,
            Quantity: 0m,
            Price: tick.Price,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            Strategy: Name,
            ConfidenceScore: 0.70,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"));

        _logger.LogInformation(
            "{Strategy} signal: {Action} {Symbol} @ {Price} | RSI={Rsi:F1} MACD-H={Histogram:F6} SL={StopLoss} TP={TakeProfit}",
            Name, signal.Action, signal.Symbol, tick.Price, rsiValue, currentHistogram, stopLoss, takeProfit);

        return signal;
    }

    public void Reset()
    {
        _rsi.Reset();
        _macd.Reset();
        _atr.Reset();
    }

    private decimal ComputeSwingStop(
        TradeAction action,
        decimal currentPrice,
        IReadOnlyList<Candle> candles,
        decimal atrValue)
    {
        int lookback = Math.Min(_settings.SwingLookback, candles.Count);
        int startIndex = candles.Count - lookback;

        if (action == TradeAction.Buy)
        {
            // Stop at recent swing low
            decimal swingLow = decimal.MaxValue;
            for (int i = startIndex; i < candles.Count; i++)
            {
                if (candles[i].Low < swingLow)
                    swingLow = candles[i].Low;
            }

            // Only use swing low if it's reasonable (within 3 ATR of price)
            if (swingLow < currentPrice && (currentPrice - swingLow) <= 3m * atrValue)
                return swingLow;

            // ATR fallback
            return currentPrice - (2m * atrValue);
        }
        else
        {
            // Stop at recent swing high
            decimal swingHigh = decimal.MinValue;
            for (int i = startIndex; i < candles.Count; i++)
            {
                if (candles[i].High > swingHigh)
                    swingHigh = candles[i].High;
            }

            if (swingHigh > currentPrice && (swingHigh - currentPrice) <= 3m * atrValue)
                return swingHigh;

            return currentPrice + (2m * atrValue);
        }
    }
}
