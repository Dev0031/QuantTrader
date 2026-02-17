namespace QuantTrader.Common.Extensions;

/// <summary>Extension methods for decimal arithmetic used in trading calculations.</summary>
public static class DecimalExtensions
{
    /// <summary>Rounds a value down to the nearest tick size increment.</summary>
    public static decimal RoundToTickSize(this decimal value, decimal tickSize)
    {
        if (tickSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(tickSize), "Tick size must be greater than zero.");

        return Math.Floor(value / tickSize) * tickSize;
    }

    /// <summary>Converts a decimal ratio (e.g. 0.05) to a percentage (e.g. 5.0).</summary>
    public static decimal ToPercentage(this decimal value) => value * 100m;

    /// <summary>Converts a percentage value (e.g. 5.0) to a decimal ratio (e.g. 0.05).</summary>
    public static decimal FromPercentage(this decimal value) => value / 100m;
}
