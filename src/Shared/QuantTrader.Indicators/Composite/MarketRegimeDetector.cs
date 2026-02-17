using QuantTrader.Common.Enums;
using QuantTrader.Indicators.MovingAverages;
using QuantTrader.Indicators.Volatility;

namespace QuantTrader.Indicators.Composite;

/// <summary>Classifies the market regime using ATR, SMA slope, and Bollinger Band width.</summary>
public sealed class MarketRegimeDetector
{
    private readonly AverageTrueRange _atr;
    private readonly SimpleMovingAverage _sma;
    private readonly SimpleMovingAverage _smaLong;
    private readonly BollingerBands _bollingerBands;
    private decimal _previousSmaValue;
    private bool _hasPreviousSma;

    /// <summary>Threshold for ATR/close ratio above which the market is considered volatile.</summary>
    public double VolatileThreshold { get; set; } = 0.03;

    /// <summary>Threshold for Bollinger BandWidth below which the market is considered calm/ranging.</summary>
    public double NarrowBandThreshold { get; set; } = 0.02;

    /// <summary>Threshold for SMA slope (percentage) above which the market is considered trending.</summary>
    public double TrendSlopeThreshold { get; set; } = 0.002;

    public MarketRegimeDetector(int atrPeriod = 14, int smaPeriod = 20, int bollingerPeriod = 20)
    {
        _atr = new AverageTrueRange(atrPeriod);
        _sma = new SimpleMovingAverage(smaPeriod);
        _smaLong = new SimpleMovingAverage(smaPeriod * 2);
        _bollingerBands = new BollingerBands(bollingerPeriod);
    }

    public bool IsReady => _atr.IsReady && _sma.IsReady && _bollingerBands.IsReady;

    /// <summary>Feed a new bar and classify the current market regime.</summary>
    public MarketRegime Detect(decimal close, decimal high, decimal low)
    {
        _atr.Update(high, low, close);
        _sma.Update(close);
        _smaLong.Update(close);
        _bollingerBands.Update(close);

        if (!IsReady)
            return MarketRegime.Calm;

        decimal atrValue = _atr.Value!.Value;
        decimal smaValue = _sma.Value!.Value;
        decimal bandWidth = _bollingerBands.BandWidth ?? 0m;

        // Compute SMA slope as percentage change from previous SMA value
        double smaSlope = 0;
        if (_hasPreviousSma && _previousSmaValue != 0m)
        {
            smaSlope = (double)Math.Abs((smaValue - _previousSmaValue) / _previousSmaValue);
        }

        _previousSmaValue = smaValue;
        _hasPreviousSma = true;

        // ATR relative to close price
        double atrRatio = close != 0m ? (double)(atrValue / close) : 0;

        // Classification priority: Volatile > Trending > Ranging > Calm
        if (atrRatio > VolatileThreshold)
            return MarketRegime.Volatile;

        if (smaSlope > TrendSlopeThreshold)
            return MarketRegime.Trending;

        if ((double)bandWidth < NarrowBandThreshold)
            return MarketRegime.Ranging;

        return MarketRegime.Calm;
    }
}
