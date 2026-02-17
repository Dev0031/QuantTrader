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
/// Bollinger Band Mean Reversion strategy.
/// Buy when price touches the lower band and RSI confirms oversold.
/// Sell when price touches the upper band and RSI confirms overbought.
/// Target: middle band (SMA). Stop-loss: 1 ATR beyond the band.
/// </summary>
public sealed class MeanReversionStrategy : IStrategy
{
    private readonly ILogger<MeanReversionStrategy> _logger;
    private readonly MeanReversionSettings _settings;
    private readonly StrategySettings _strategySettings;

    private readonly BollingerBands _bb;
    private readonly RelativeStrengthIndex _rsi;
    private readonly AverageTrueRange _atr;

    public MeanReversionStrategy(
        ILogger<MeanReversionStrategy> logger,
        IOptions<MeanReversionSettings> settings,
        IOptions<StrategySettings> strategySettings)
    {
        _logger = logger;
        _settings = settings.Value;
        _strategySettings = strategySettings.Value;

        _bb = new BollingerBands(_settings.BollingerPeriod, _settings.BollingerStdDev);
        _rsi = new RelativeStrengthIndex(_settings.RsiPeriod);
        _atr = new AverageTrueRange(_settings.AtrPeriod);
    }

    public string Name => "MeanReversion";

    public bool IsEnabled =>
        _strategySettings.EnabledStrategies.Contains(Name, StringComparer.OrdinalIgnoreCase);

    public TradeSignal? Evaluate(MarketTick tick, IReadOnlyList<Candle> recentCandles)
    {
        if (recentCandles.Count == 0)
            return null;

        // Rebuild indicators from candle history
        _bb.Reset();
        _rsi.Reset();
        _atr.Reset();

        foreach (var candle in recentCandles)
        {
            _bb.Update(candle.Close);
            _rsi.Update(candle.Close);
            _atr.Update(candle.High, candle.Low, candle.Close);
        }

        if (!_bb.IsReady || !_rsi.IsReady || !_atr.IsReady)
        {
            _logger.LogDebug("{Strategy}: Indicators not ready for {Symbol}", Name, tick.Symbol);
            return null;
        }

        decimal upperBand = _bb.UpperBand!.Value;
        decimal lowerBand = _bb.LowerBand!.Value;
        decimal middleBand = _bb.MiddleBand!.Value;
        double rsiValue = _rsi.Value!.Value;
        decimal atrValue = _atr.Value!.Value;
        decimal price = tick.Price;

        TradeAction? action = null;
        decimal stopLoss;
        decimal takeProfit;

        // Buy: price at or below lower band AND RSI oversold
        if (price <= lowerBand && rsiValue < _settings.RsiOversold)
        {
            action = TradeAction.Buy;
            stopLoss = lowerBand - (_settings.AtrStopMultiplier * atrValue);
            takeProfit = middleBand;
        }
        // Sell: price at or above upper band AND RSI overbought
        else if (price >= upperBand && rsiValue > _settings.RsiOverbought)
        {
            action = TradeAction.Sell;
            stopLoss = upperBand + (_settings.AtrStopMultiplier * atrValue);
            takeProfit = middleBand;
        }
        else
        {
            return null;
        }

        var signal = new TradeSignal(
            Id: Guid.NewGuid(),
            Symbol: tick.Symbol,
            Action: action.Value,
            Quantity: 0m,
            Price: price,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            Strategy: Name,
            ConfidenceScore: 0.70,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"));

        _logger.LogInformation(
            "{Strategy} signal: {Action} {Symbol} @ {Price} | RSI={Rsi:F1} BB=[{Lower:F2},{Upper:F2}] SL={StopLoss} TP={TakeProfit}",
            Name, signal.Action, signal.Symbol, price, rsiValue, lowerBand, upperBand, stopLoss, takeProfit);

        return signal;
    }

    public void Reset()
    {
        _bb.Reset();
        _rsi.Reset();
        _atr.Reset();
    }
}
