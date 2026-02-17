namespace QuantTrader.Indicators.Composite;

/// <summary>Aggregates multiple indicator signals into a single weighted confidence score.</summary>
public sealed class ConfluenceScorer
{
    private readonly Dictionary<string, double> _weights = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Add or update the weight for a named indicator signal.</summary>
    public void AddWeight(string indicatorName, double weight)
    {
        ArgumentNullException.ThrowIfNull(indicatorName);
        ArgumentOutOfRangeException.ThrowIfNegative(weight);
        _weights[indicatorName] = weight;
    }

    /// <summary>Remove a weight entry for the given indicator.</summary>
    public bool RemoveWeight(string indicatorName)
    {
        return _weights.Remove(indicatorName);
    }

    /// <summary>
    /// Compute a weighted average confidence score from the provided signals.
    /// Signals should be in the range [0.0, 1.0]. Unknown signals (not in weights) are ignored.
    /// Returns 0.0 to 1.0, clamped.
    /// </summary>
    public double Score(IReadOnlyDictionary<string, double> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        double weightedSum = 0;
        double totalWeight = 0;

        foreach (var kvp in signals)
        {
            if (!_weights.TryGetValue(kvp.Key, out double weight))
                continue;

            double signal = kvp.Value;

            // Clamp signal to [0, 1]
            if (signal < 0.0) signal = 0.0;
            else if (signal > 1.0) signal = 1.0;

            weightedSum += signal * weight;
            totalWeight += weight;
        }

        if (totalWeight == 0)
            return 0.0;

        double score = weightedSum / totalWeight;

        // Clamp result to [0, 1]
        if (score < 0.0) return 0.0;
        if (score > 1.0) return 1.0;
        return score;
    }
}
