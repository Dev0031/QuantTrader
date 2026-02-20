using System.Collections.Concurrent;
using QuantTrader.Infrastructure.Messaging;

namespace QuantTrader.TestInfrastructure.Fakes;

/// <summary>
/// In-memory event bus for unit and component tests.
/// Delivers events synchronously to registered handlers (no async queue).
/// Captures all published events for assertion via <see cref="PublishedEvents{T}"/>.
/// </summary>
public sealed class FakeEventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, List<Func<object, CancellationToken, Task>>> _handlers = new();
    private readonly ConcurrentBag<(string Topic, object Event)> _published = new();

    /// <summary>Returns all events published to the given topic, filtered by type T.</summary>
    public IReadOnlyList<T> PublishedEvents<T>(string topic)
        => _published
            .Where(p => p.Topic == topic && p.Event is T)
            .Select(p => (T)p.Event)
            .ToList();

    /// <summary>Returns all events published across all topics, filtered by type T.</summary>
    public IReadOnlyList<T> AllPublished<T>()
        => _published
            .Where(p => p.Event is T)
            .Select(p => (T)p.Event)
            .ToList();

    /// <summary>Clears all published events and handlers. Call between tests.</summary>
    public void Reset()
    {
        _handlers.Clear();
        while (_published.TryTake(out _)) { }
    }

    public Task PublishAsync<TEvent>(TEvent @event, string topic, CancellationToken ct = default) where TEvent : class
    {
        _published.Add((topic, @event));

        if (_handlers.TryGetValue(topic, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                handler(@event, ct).GetAwaiter().GetResult(); // Synchronous delivery for test determinism
            }
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<TEvent>(string topic, Func<TEvent, CancellationToken, Task> handler, CancellationToken ct = default) where TEvent : class
    {
        _handlers.AddOrUpdate(
            topic,
            _ => [WrapHandler(handler)],
            (_, existing) => { existing.Add(WrapHandler(handler)); return existing; });

        return Task.CompletedTask;
    }

    private static Func<object, CancellationToken, Task> WrapHandler<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class
        => (obj, ct) => obj is TEvent evt ? handler(evt, ct) : Task.CompletedTask;

}
