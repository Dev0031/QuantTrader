using QuantTrader.Indicators.MovingAverages;

namespace QuantTrader.Indicators.Volatility;

/// <summary>Bollinger Bands using SMA and standard deviation for dynamic support/resistance.</summary>
public sealed class BollingerBands : IIndicator
{
    private readonly SimpleMovingAverage _sma;
    private readonly int _period;
    private readonly double _standardDeviations;
    private readonly decimal[] _buffer;
    private int _head;
    private int _count;

    public BollingerBands(int period = 20, double standardDeviations = 2.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(standardDeviations, 0.0);

        _period = period;
        _standardDeviations = standardDeviations;
        _sma = new SimpleMovingAverage(period);
        _buffer = new decimal[period];
    }

    public string Name => $"BB({_period},{_standardDeviations:F1})";
    public int Period => _period;
    public bool IsReady => _sma.IsReady;

    public decimal? UpperBand { get; private set; }
    public decimal? MiddleBand { get; private set; }
    public decimal? LowerBand { get; private set; }

    /// <summary>Width of the bands relative to the middle band.</summary>
    public decimal? BandWidth =>
        UpperBand.HasValue && LowerBand.HasValue && MiddleBand.HasValue && MiddleBand.Value != 0m
            ? (UpperBand.Value - LowerBand.Value) / MiddleBand.Value
            : null;

    public void Update(decimal value)
    {
        // Maintain our own circular buffer for std dev calculation
        if (_count >= _period)
        {
            // Overwrite oldest
        }
        else
        {
            _count++;
        }

        _buffer[_head] = value;
        _head = (_head + 1) % _period;

        _sma.Update(value);

        if (!_sma.IsReady)
            return;

        decimal mean = _sma.Value!.Value;
        MiddleBand = mean;

        // Compute standard deviation without LINQ
        double sumSquares = 0;
        int len = _count < _period ? _count : _period;
        for (int i = 0; i < len; i++)
        {
            double diff = (double)(_buffer[i] - mean);
            sumSquares += diff * diff;
        }

        double stdDev = Math.Sqrt(sumSquares / len);
        decimal deviation = (decimal)(stdDev * _standardDeviations);

        UpperBand = mean + deviation;
        LowerBand = mean - deviation;
    }

    public void Reset()
    {
        _sma.Reset();
        _head = 0;
        _count = 0;
        Array.Clear(_buffer);
        UpperBand = null;
        MiddleBand = null;
        LowerBand = null;
    }
}
