using QuantTrader.Indicators.MovingAverages;

namespace QuantTrader.Indicators.Oscillators;

/// <summary>Moving Average Convergence Divergence using fast/slow EMAs and a signal line.</summary>
public sealed class MACD : IIndicator
{
    private readonly ExponentialMovingAverage _fastEma;
    private readonly ExponentialMovingAverage _slowEma;
    private readonly ExponentialMovingAverage _signalEma;
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly int _signalPeriod;

    public MACD(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fastPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(slowPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(signalPeriod, 1);

        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("fastPeriod must be less than slowPeriod.");

        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
        _signalPeriod = signalPeriod;

        _fastEma = new ExponentialMovingAverage(fastPeriod);
        _slowEma = new ExponentialMovingAverage(slowPeriod);
        _signalEma = new ExponentialMovingAverage(signalPeriod);
    }

    public string Name => $"MACD({_fastPeriod},{_slowPeriod},{_signalPeriod})";
    public bool IsReady => _slowEma.IsReady && _signalEma.IsReady;

    /// <summary>Difference between fast and slow EMAs.</summary>
    public decimal? MACDLine =>
        _fastEma.IsReady && _slowEma.IsReady
            ? _fastEma.Value!.Value - _slowEma.Value!.Value
            : null;

    /// <summary>EMA of the MACD line.</summary>
    public decimal? SignalLine => _signalEma.IsReady ? _signalEma.Value : null;

    /// <summary>Difference between MACD line and signal line.</summary>
    public decimal? Histogram =>
        MACDLine.HasValue && SignalLine.HasValue
            ? MACDLine.Value - SignalLine.Value
            : null;

    public void Update(decimal value)
    {
        _fastEma.Update(value);
        _slowEma.Update(value);

        if (_fastEma.IsReady && _slowEma.IsReady)
        {
            decimal macdLine = _fastEma.Value!.Value - _slowEma.Value!.Value;
            _signalEma.Update(macdLine);
        }
    }

    public void Reset()
    {
        _fastEma.Reset();
        _slowEma.Reset();
        _signalEma.Reset();
    }
}
