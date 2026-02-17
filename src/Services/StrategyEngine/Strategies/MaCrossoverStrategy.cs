using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Indicators.MovingAverages;
using QuantTrader.Indicators.Volatility;
using QuantTrader.StrategyEngine.Configuration;

namespace QuantTrader.StrategyEngine.Strategies;

/// <summary>
/// Moving Average Crossover strategy.
/// Generates a Buy signal when the fast SMA crosses above the slow SMA,
/// and a Sell signal when the fast SMA crosses below the slow SMA.
/// Stop-loss is placed at 2x ATR below entry; take-profit at 3x ATR above entry.
/// </summary>
public sealed class MaCrossoverStrategy : IStrategy
{
    private readonly ILogger<MaCrossoverStrategy> _logger;
    private readonly MaCrossoverSettings _settings;
    private readonly StrategySettings _strategySettings;

    private readonly SimpleMovingAverage _fastSma;
    private readonly SimpleMovingAverage _slowSma;
    private readonly AverageTrueRange _atr;

    private decimal? _previousFastValue;
    private decimal? _previousSlowValue;

    public MaCrossoverStrategy(
        ILogger<MaCrossoverStrategy> logger,
        IOptions<MaCrossoverSettings> settings,
        IOptions<StrategySettings> strategySettings)
    {
        _logger = logger;
        _settings = settings.Value;
        _strategySettings = strategySettings.Value;

        _fastSma = new SimpleMovingAverage(_settings.FastPeriod);
        _slowSma = new SimpleMovingAverage(_settings.SlowPeriod);
        _atr = new AverageTrueRange(_settings.AtrPeriod);
    }

    public string Name => "MaCrossover";

    public bool IsEnabled =>
        _strategySettings.EnabledStrategies.Contains(Name, StringComparer.OrdinalIgnoreCase);

    public TradeSignal? Evaluate(MarketTick tick, IReadOnlyList<Candle> recentCandles)
    {
        if (recentCandles.Count == 0)
            return null;

        // Feed all candle closes into the indicators (rebuild from scratch for consistency)
        _fastSma.Reset();
        _slowSma.Reset();
        _atr.Reset();
        _previousFastValue = null;
        _previousSlowValue = null;

        for (int i = 0; i < recentCandles.Count; i++)
        {
            var candle = recentCandles[i];
            decimal close = candle.Close;

            // Track previous SMA values before the last candle update
            if (i == recentCandles.Count - 2)
            {
                _previousFastValue = _fastSma.Value;
                _previousSlowValue = _slowSma.Value;
            }

            _fastSma.Update(close);
            _slowSma.Update(close);
            _atr.Update(candle.High, candle.Low, candle.Close);
        }

        if (!_fastSma.IsReady || !_slowSma.IsReady || !_atr.IsReady)
        {
            _logger.LogDebug("{Strategy}: Indicators not ready for {Symbol}. Need more candles", Name, tick.Symbol);
            return null;
        }

        decimal currentFast = _fastSma.Value!.Value;
        decimal currentSlow = _slowSma.Value!.Value;
        decimal atrValue = _atr.Value!.Value;

        if (!_previousFastValue.HasValue || !_previousSlowValue.HasValue)
            return null;

        bool previousFastBelowSlow = _previousFastValue.Value < _previousSlowValue.Value;
        bool previousFastAboveSlow = _previousFastValue.Value > _previousSlowValue.Value;
        bool currentFastAboveSlow = currentFast > currentSlow;
        bool currentFastBelowSlow = currentFast < currentSlow;

        TradeAction? action = null;

        // Bullish crossover: fast crosses above slow
        if (previousFastBelowSlow && currentFastAboveSlow)
        {
            action = TradeAction.Buy;
        }
        // Bearish crossover: fast crosses below slow
        else if (previousFastAboveSlow && currentFastBelowSlow)
        {
            action = TradeAction.Sell;
        }

        if (action is null)
            return null;

        decimal stopLoss = action == TradeAction.Buy
            ? tick.Price - (_settings.AtrStopMultiplier * atrValue)
            : tick.Price + (_settings.AtrStopMultiplier * atrValue);

        decimal takeProfit = action == TradeAction.Buy
            ? tick.Price + (_settings.AtrProfitMultiplier * atrValue)
            : tick.Price - (_settings.AtrProfitMultiplier * atrValue);

        var signal = new TradeSignal(
            Id: Guid.NewGuid(),
            Symbol: tick.Symbol,
            Action: action.Value,
            Quantity: 0m, // Quantity determined downstream by position sizing
            Price: tick.Price,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            Strategy: Name,
            ConfidenceScore: 0.75,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"));

        _logger.LogInformation(
            "{Strategy} signal: {Action} {Symbol} @ {Price} | SL={StopLoss} TP={TakeProfit} ATR={Atr}",
            Name, signal.Action, signal.Symbol, tick.Price, stopLoss, takeProfit, atrValue);

        return signal;
    }

    public void Reset()
    {
        _fastSma.Reset();
        _slowSma.Reset();
        _atr.Reset();
        _previousFastValue = null;
        _previousSlowValue = null;
    }
}
