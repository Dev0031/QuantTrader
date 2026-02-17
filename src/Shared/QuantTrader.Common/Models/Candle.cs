namespace QuantTrader.Common.Models;

/// <summary>Represents an OHLCV candlestick for a given symbol and interval.</summary>
public sealed record Candle(
    string Symbol,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    string Interval);
