namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for an OHLCV candlestick.</summary>
public sealed record CandleResponse(
    string Symbol,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    string Interval);
