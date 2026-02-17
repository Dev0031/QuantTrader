namespace QuantTrader.Indicators.Oscillators;

/// <summary>Relative Strength Index using Wilder's smoothing method.</summary>
public sealed class RelativeStrengthIndex : IIndicator
{
    private readonly int _period;
    private readonly double[] _gainBuffer;
    private readonly double[] _lossBuffer;
    private int _count;
    private int _bufferIndex;
    private double _avgGain;
    private double _avgLoss;
    private double _previousValue;
    private bool _hasPrevious;

    public RelativeStrengthIndex(int period = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _period = period;
        _gainBuffer = new double[period];
        _lossBuffer = new double[period];
    }

    public string Name => $"RSI({_period})";
    public int Period => _period;

    /// <summary>Requires period + 1 data points (one for the initial delta).</summary>
    public bool IsReady => _count > _period;

    public double? Value { get; private set; }

    /// <summary>True when RSI is above 70.</summary>
    public bool IsOverbought => Value.HasValue && Value.Value > 70.0;

    /// <summary>True when RSI is below 30.</summary>
    public bool IsOversold => Value.HasValue && Value.Value < 30.0;

    public void Update(decimal value)
    {
        double val = (double)value;

        if (!_hasPrevious)
        {
            _previousValue = val;
            _hasPrevious = true;
            return;
        }

        double change = val - _previousValue;
        _previousValue = val;

        double gain = change > 0 ? change : 0;
        double loss = change < 0 ? -change : 0;

        _count++;

        if (_count <= _period)
        {
            // Accumulation phase: fill the buffer for initial average
            _gainBuffer[_bufferIndex] = gain;
            _lossBuffer[_bufferIndex] = loss;
            _bufferIndex++;

            if (_count == _period)
            {
                // Compute initial averages from buffered values
                double totalGain = 0;
                double totalLoss = 0;
                for (int i = 0; i < _period; i++)
                {
                    totalGain += _gainBuffer[i];
                    totalLoss += _lossBuffer[i];
                }

                _avgGain = totalGain / _period;
                _avgLoss = totalLoss / _period;
                ComputeRsi();
            }
        }
        else
        {
            // Wilder's smoothing
            _avgGain = (_avgGain * (_period - 1) + gain) / _period;
            _avgLoss = (_avgLoss * (_period - 1) + loss) / _period;
            ComputeRsi();
        }
    }

    private void ComputeRsi()
    {
        if (_avgLoss == 0)
        {
            Value = 100.0;
            return;
        }

        double rs = _avgGain / _avgLoss;
        Value = 100.0 - (100.0 / (1.0 + rs));
    }

    public void Reset()
    {
        _count = 0;
        _bufferIndex = 0;
        _avgGain = 0;
        _avgLoss = 0;
        _previousValue = 0;
        _hasPrevious = false;
        Value = null;
        Array.Clear(_gainBuffer);
        Array.Clear(_lossBuffer);
    }
}
