namespace QuantTrader.StrategyEngine.Configuration;

/// <summary>Configuration for the Volatility Breakout strategy.</summary>
public sealed class BreakoutSettings
{
    public const string SectionName = "Breakout";

    public int RangePeriod { get; set; } = 20;
    public decimal VolumeMultiplier { get; set; } = 1.5m;
    public int AtrPeriod { get; set; } = 14;
    public decimal AtrStopMultiplier { get; set; } = 1.5m;
    public decimal AtrProfitMultiplier { get; set; } = 3.0m;
}
