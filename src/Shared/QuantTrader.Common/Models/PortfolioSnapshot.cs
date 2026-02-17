namespace QuantTrader.Common.Models;

/// <summary>Represents a point-in-time snapshot of the trading portfolio.</summary>
public sealed record PortfolioSnapshot(
    decimal TotalEquity,
    decimal AvailableBalance,
    decimal TotalUnrealizedPnl,
    decimal TotalRealizedPnl,
    double DrawdownPercent,
    IReadOnlyList<Position> Positions,
    DateTimeOffset Timestamp);
