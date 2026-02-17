using QuantTrader.Common.Models;

namespace QuantTrader.Common.Events;

/// <summary>Event raised when a new market tick is received from the exchange.</summary>
public sealed record MarketTickReceivedEvent(
    MarketTick Tick,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source);
