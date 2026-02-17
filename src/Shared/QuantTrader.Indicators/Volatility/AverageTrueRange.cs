namespace QuantTrader.Indicators.Volatility;

/// <summary>Average True Range using Wilder's smoothing for volatility measurement.</summary>
public sealed class AverageTrueRange : IIndicator
{
    private readonly int _period;
    private decimal _previousClose;
    private bool _hasPrevious;
    private int _count;
    private decimal _trSum;

    public AverageTrueRange(int period = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _period = period;
    }

    public string Name => $"ATR({_period})";
    public int Period => _period;
    public bool IsReady => _count >= _period;
    public decimal? Value { get; private set; }

    /// <summary>Feed a new bar's high, low, and close to compute the true range.</summary>
    public void Update(decimal high, decimal low, decimal close)
    {
        decimal trueRange;

        if (!_hasPrevious)
        {
            trueRange = high - low;
            _hasPrevious = true;
        }
        else
        {
            // True Range = max(H-L, |H-PrevC|, |L-PrevC|)
            decimal hl = high - low;
            decimal hpc = Math.Abs(high - _previousClose);
            decimal lpc = Math.Abs(low - _previousClose);
            trueRange = Math.Max(hl, Math.Max(hpc, lpc));
        }

        _previousClose = close;
        _count++;

        if (_count <= _period)
        {
            _trSum += trueRange;

            if (_count == _period)
            {
                Value = _trSum / _period;
            }
        }
        else
        {
            // Wilder's smoothing: ATR = (prevATR * (period-1) + TR) / period
            Value = (Value!.Value * (_period - 1) + trueRange) / _period;
        }
    }

    /// <summary>IIndicator.Update - uses the value as close with zero range. Prefer the (high, low, close) overload.</summary>
    public void Update(decimal value)
    {
        Update(value, value, value);
    }

    public void Reset()
    {
        _previousClose = 0m;
        _hasPrevious = false;
        _count = 0;
        _trSum = 0m;
        Value = null;
    }
}
