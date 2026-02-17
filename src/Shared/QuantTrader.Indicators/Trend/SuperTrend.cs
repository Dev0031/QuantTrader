using QuantTrader.Indicators.Volatility;

namespace QuantTrader.Indicators.Trend;

/// <summary>SuperTrend indicator combining ATR with trend direction for dynamic stop-loss levels.</summary>
public sealed class SuperTrend : IIndicator
{
    private readonly AverageTrueRange _atr;
    private readonly int _period;
    private readonly decimal _multiplier;
    private decimal _upperBand;
    private decimal _lowerBand;
    private decimal _previousClose;
    private decimal _previousUpperBand;
    private decimal _previousLowerBand;
    private bool _hasPrevious;

    public SuperTrend(int period = 10, double multiplier = 3.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _period = period;
        _multiplier = (decimal)multiplier;
        _atr = new AverageTrueRange(period);
    }

    public string Name => $"SuperTrend({_period},{_multiplier:F1})";
    public bool IsReady => _atr.IsReady && _hasPrevious;
    public decimal? Value { get; private set; }
    public bool IsUpTrend { get; private set; } = true;

    /// <summary>Feed a new bar to update the SuperTrend. Prefer this overload over Update(decimal).</summary>
    public void Update(decimal high, decimal low, decimal close)
    {
        _atr.Update(high, low, close);

        if (!_atr.IsReady)
        {
            _previousClose = close;
            _hasPrevious = true;
            return;
        }

        decimal atrValue = _atr.Value!.Value;
        decimal midPoint = (high + low) / 2m;
        decimal basicUpper = midPoint + _multiplier * atrValue;
        decimal basicLower = midPoint - _multiplier * atrValue;

        // Adjust bands based on previous bands
        if (_hasPrevious && _previousUpperBand != 0)
        {
            _upperBand = basicUpper < _previousUpperBand || _previousClose > _previousUpperBand
                ? basicUpper
                : _previousUpperBand;

            _lowerBand = basicLower > _previousLowerBand || _previousClose < _previousLowerBand
                ? basicLower
                : _previousLowerBand;
        }
        else
        {
            _upperBand = basicUpper;
            _lowerBand = basicLower;
        }

        // Determine trend direction
        if (Value.HasValue)
        {
            if (IsUpTrend && close < _lowerBand)
            {
                IsUpTrend = false;
            }
            else if (!IsUpTrend && close > _upperBand)
            {
                IsUpTrend = true;
            }
        }
        else
        {
            IsUpTrend = close > _upperBand;
        }

        Value = IsUpTrend ? _lowerBand : _upperBand;

        _previousClose = close;
        _previousUpperBand = _upperBand;
        _previousLowerBand = _lowerBand;
        _hasPrevious = true;
    }

    /// <summary>IIndicator.Update - uses value as high, low, and close. Prefer the (high, low, close) overload.</summary>
    public void Update(decimal value)
    {
        Update(value, value, value);
    }

    public void Reset()
    {
        _atr.Reset();
        _upperBand = 0m;
        _lowerBand = 0m;
        _previousClose = 0m;
        _previousUpperBand = 0m;
        _previousLowerBand = 0m;
        _hasPrevious = false;
        IsUpTrend = true;
        Value = null;
    }
}
