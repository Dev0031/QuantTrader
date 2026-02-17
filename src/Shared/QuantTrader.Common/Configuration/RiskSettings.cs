namespace QuantTrader.Common.Configuration;

/// <summary>Configuration settings for risk management and position limits.</summary>
public sealed class RiskSettings
{
    public const string SectionName = "Risk";

    public double MaxRiskPerTradePercent { get; set; } = 2.0;
    public double MaxDrawdownPercent { get; set; } = 5.0;
    public double MinRiskRewardRatio { get; set; } = 2.0;
    public int MaxOpenPositions { get; set; } = 5;
    public decimal MaxDailyLoss { get; set; }
    public bool KillSwitchEnabled { get; set; } = true;
}
