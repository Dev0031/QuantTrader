namespace QuantTrader.StrategyEngine.Configuration;

/// <summary>Configuration for the Bollinger Band Mean Reversion strategy.</summary>
public sealed class MeanReversionSettings
{
    public const string SectionName = "MeanReversion";

    public int BollingerPeriod { get; set; } = 20;
    public double BollingerStdDev { get; set; } = 2.0;
    public int RsiPeriod { get; set; } = 14;
    public double RsiOversold { get; set; } = 35.0;
    public double RsiOverbought { get; set; } = 65.0;
    public int AtrPeriod { get; set; } = 14;
    public decimal AtrStopMultiplier { get; set; } = 1.0m;
}
