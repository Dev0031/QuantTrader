namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for an open trading position.</summary>
public sealed record PositionResponse(
    string Symbol,
    string Side,
    decimal EntryPrice,
    decimal CurrentPrice,
    decimal Quantity,
    decimal UnrealizedPnl,
    decimal RealizedPnl,
    decimal? StopLoss,
    decimal? TakeProfit,
    DateTimeOffset OpenedAt);
