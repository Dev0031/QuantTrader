using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace QuantTrader.Infrastructure.Messaging;

/// <summary>
/// In-memory implementation of <see cref="IEventBus"/> for local development and testing.
/// Uses a ConcurrentDictionary of handler lists to dispatch events.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ConcurrentDictionary<string, List<Func<string, CancellationToken, Task>>> _handlers = new();
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(T @event, string topic, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var json = JsonSerializer.Serialize(@event, JsonOptions);
        _logger.LogDebug("InMemory publish {EventType} to topic {Topic}", typeof(T).Name, topic);

        if (!_handlers.TryGetValue(topic, out var handlers))
        {
            return;
        }

        // Snapshot the handler list to avoid issues if a handler modifies the collection.
        List<Func<string, CancellationToken, Task>> snapshot;
        lock (handlers)
        {
            snapshot = [.. handlers];
        }

        foreach (var handler in snapshot)
        {
            try
            {
                await handler(json, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in InMemory handler for topic {Topic}", topic);
            }
        }
    }

    public Task SubscribeAsync<T>(string topic, Func<T, CancellationToken, Task> handler, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        var handlers = _handlers.GetOrAdd(topic, _ => []);

        lock (handlers)
        {
            handlers.Add(async (json, token) =>
            {
                var @event = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (@event is not null)
                {
                    await handler(@event, token).ConfigureAwait(false);
                }
            });
        }

        _logger.LogInformation("InMemory subscription registered for topic {Topic} with handler type {HandlerType}", topic, typeof(T).Name);
        return Task.CompletedTask;
    }
}
