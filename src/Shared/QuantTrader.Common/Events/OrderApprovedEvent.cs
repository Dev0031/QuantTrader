using QuantTrader.Common.Models;

namespace QuantTrader.Common.Events;

/// <summary>Event raised when an order passes risk validation and is approved.</summary>
public sealed record OrderApprovedEvent(
    Order Order,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source);
