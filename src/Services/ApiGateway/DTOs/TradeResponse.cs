namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for a completed or open trade.</summary>
public sealed record TradeResponse(
    Guid Id,
    string Symbol,
    string Side,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Quantity,
    decimal RealizedPnl,
    decimal Commission,
    string Strategy,
    DateTimeOffset EntryTime,
    DateTimeOffset? ExitTime,
    string Status);
