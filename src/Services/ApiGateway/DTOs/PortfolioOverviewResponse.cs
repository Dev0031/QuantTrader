namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for the dashboard portfolio overview.</summary>
public sealed record PortfolioOverviewResponse(
    decimal TotalEquity,
    decimal AvailableBalance,
    decimal TotalUnrealizedPnl,
    decimal TotalRealizedPnl,
    double DrawdownPercent,
    int ActivePositionCount,
    decimal TodayPnl,
    DateTimeOffset Timestamp);
