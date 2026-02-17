namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for current risk metrics.</summary>
public sealed record RiskMetricsResponse(
    double CurrentDrawdownPercent,
    double MaxDrawdownPercent,
    decimal TotalExposure,
    int OpenPositionCount,
    int MaxOpenPositions,
    decimal DailyLoss,
    decimal MaxDailyLoss,
    bool KillSwitchActive,
    DateTimeOffset Timestamp);
