using QuantTrader.Common.Models;

namespace QuantTrader.Common.Events;

/// <summary>Event raised when a strategy generates a new trade signal.</summary>
public sealed record TradeSignalGeneratedEvent(
    TradeSignal Signal,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source);
