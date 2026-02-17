namespace QuantTrader.Common.Events;

/// <summary>Event raised when the kill switch activates to halt all trading.</summary>
public sealed record KillSwitchTriggeredEvent(
    string Reason,
    double DrawdownPercent,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source);
