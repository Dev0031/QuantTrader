namespace QuantTrader.Infrastructure.Messaging;

/// <summary>Abstraction for publish/subscribe event bus used across all microservices.</summary>
public interface IEventBus
{
    /// <summary>Publishes an event to the specified topic.</summary>
    Task PublishAsync<T>(T @event, string topic, CancellationToken ct = default) where T : class;

    /// <summary>Subscribes to events on the specified topic, invoking the handler for each received event.</summary>
    Task SubscribeAsync<T>(string topic, Func<T, CancellationToken, Task> handler, CancellationToken ct = default) where T : class;
}
