namespace QuantTrader.Common.Configuration;

/// <summary>Configuration settings for strategy selection and thresholds.</summary>
public sealed class StrategySettings
{
    public const string SectionName = "Strategy";

    public List<string> EnabledStrategies { get; set; } = [];
    public string DefaultTimeframe { get; set; } = "1h";
    public double MinConfidenceScore { get; set; } = 0.7;
}
