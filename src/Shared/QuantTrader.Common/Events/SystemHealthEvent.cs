namespace QuantTrader.Common.Events;

/// <summary>Marker interface for all domain events.</summary>
public interface IEvent { }

/// <summary>Published by a service when its health status changes (e.g., dependency goes down).</summary>
public sealed record SystemHealthEvent(
    string Service,
    string Component,
    HealthStatus Status,
    string Message,
    string CorrelationId,
    DateTimeOffset Timestamp,
    string Source) : IEvent;

/// <summary>Health status levels for system components.</summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}
