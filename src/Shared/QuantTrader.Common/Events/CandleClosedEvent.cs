using QuantTrader.Common.Models;

namespace QuantTrader.Common.Events;

/// <summary>Event raised when a candlestick interval closes.</summary>
public sealed record CandleClosedEvent(
    Candle Candle,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source);
