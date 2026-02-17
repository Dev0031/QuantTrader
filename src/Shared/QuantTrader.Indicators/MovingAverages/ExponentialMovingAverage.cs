namespace QuantTrader.Indicators.MovingAverages;

/// <summary>Exponential Moving Average with configurable smoothing multiplier.</summary>
public sealed class ExponentialMovingAverage : IIndicator
{
    private readonly int _period;
    private readonly decimal _multiplier;
    private decimal _sum;
    private int _count;

    public ExponentialMovingAverage(int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _period = period;
        _multiplier = 2.0m / (period + 1);
    }

    public string Name => $"EMA({_period})";
    public int Period => _period;
    public bool IsReady => _count >= _period;
    public decimal? Value => _count > 0 ? _emaValue : null;

    private decimal _emaValue;

    public void Update(decimal value)
    {
        _count++;

        if (_count <= _period)
        {
            // Accumulate values for the initial SMA seed
            _sum += value;

            if (_count == _period)
            {
                _emaValue = _sum / _period;
            }
        }
        else
        {
            _emaValue = (value - _emaValue) * _multiplier + _emaValue;
        }
    }

    public void Reset()
    {
        _count = 0;
        _sum = 0m;
        _emaValue = 0m;
    }
}
