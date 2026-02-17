namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for a market tick.</summary>
public sealed record TickResponse(
    string Symbol,
    decimal Price,
    decimal Volume,
    decimal BidPrice,
    decimal AskPrice,
    DateTimeOffset Timestamp);
