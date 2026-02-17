namespace QuantTrader.Indicators.MovingAverages;

/// <summary>Simple Moving Average using a circular buffer for O(1) updates.</summary>
public sealed class SimpleMovingAverage : IIndicator
{
    private readonly decimal[] _buffer;
    private readonly int _period;
    private int _head;
    private int _count;
    private decimal _sum;

    public SimpleMovingAverage(int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _period = period;
        _buffer = new decimal[period];
    }

    public string Name => $"SMA({_period})";
    public int Period => _period;
    public bool IsReady => _count >= _period;
    public decimal? Value => IsReady ? _sum / _period : null;

    public void Update(decimal value)
    {
        if (_count >= _period)
        {
            _sum -= _buffer[_head];
        }
        else
        {
            _count++;
        }

        _buffer[_head] = value;
        _sum += value;
        _head = (_head + 1) % _period;
    }

    public void Reset()
    {
        _head = 0;
        _count = 0;
        _sum = 0m;
        Array.Clear(_buffer);
    }

    /// <summary>Copies the current buffer contents into the destination span in chronological order.</summary>
    public int CopyBufferTo(Span<decimal> destination)
    {
        if (!IsReady || destination.Length < _period)
            return 0;

        // _head points to the oldest element when buffer is full
        int start = _head;
        for (int i = 0; i < _period; i++)
        {
            destination[i] = _buffer[(start + i) % _period];
        }

        return _period;
    }
}
