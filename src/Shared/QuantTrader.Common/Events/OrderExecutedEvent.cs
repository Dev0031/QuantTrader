using QuantTrader.Common.Models;

namespace QuantTrader.Common.Events;

/// <summary>Event raised when an order has been executed on the exchange.</summary>
public sealed record OrderExecutedEvent(
    Order Order,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source);
