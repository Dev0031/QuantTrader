namespace QuantTrader.Indicators;

/// <summary>Common interface for all technical analysis indicators.</summary>
public interface IIndicator
{
    /// <summary>The display name of this indicator.</summary>
    string Name { get; }

    /// <summary>Whether the indicator has received enough data to produce valid output.</summary>
    bool IsReady { get; }

    /// <summary>Feed a new price value into the indicator.</summary>
    void Update(decimal value);

    /// <summary>Reset the indicator to its initial state.</summary>
    void Reset();
}
