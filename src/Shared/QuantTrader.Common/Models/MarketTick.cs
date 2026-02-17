namespace QuantTrader.Common.Models;

/// <summary>Represents a real-time market tick with bid/ask pricing data.</summary>
public sealed record MarketTick(
    string Symbol,
    decimal Price,
    decimal Volume,
    decimal BidPrice,
    decimal AskPrice,
    DateTimeOffset Timestamp);
