namespace QuantTrader.Common.Events;

/// <summary>Event raised when a risk threshold is breached or approaching.</summary>
public sealed record RiskAlertEvent(
    string AlertType,
    string Message,
    string Symbol,
    double Severity,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source);
