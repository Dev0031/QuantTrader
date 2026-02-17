namespace QuantTrader.StrategyEngine.Configuration;

/// <summary>Configuration for the Moving Average Crossover strategy.</summary>
public sealed class MaCrossoverSettings
{
    public const string SectionName = "MaCrossover";

    public int FastPeriod { get; set; } = 20;
    public int SlowPeriod { get; set; } = 50;
    public int AtrPeriod { get; set; } = 14;
    public decimal AtrStopMultiplier { get; set; } = 2.0m;
    public decimal AtrProfitMultiplier { get; set; } = 3.0m;
}
