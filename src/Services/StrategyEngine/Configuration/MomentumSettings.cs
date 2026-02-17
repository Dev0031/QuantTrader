namespace QuantTrader.StrategyEngine.Configuration;

/// <summary>Configuration for the RSI + MACD Momentum strategy.</summary>
public sealed class MomentumSettings
{
    public const string SectionName = "Momentum";

    public double RsiOversold { get; set; } = 30.0;
    public double RsiOverbought { get; set; } = 70.0;
    public int RsiPeriod { get; set; } = 14;
    public int MacdFastPeriod { get; set; } = 12;
    public int MacdSlowPeriod { get; set; } = 26;
    public int MacdSignalPeriod { get; set; } = 9;
    public int AtrPeriod { get; set; } = 14;
    public int SwingLookback { get; set; } = 10;
}
