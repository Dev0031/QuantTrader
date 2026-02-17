using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace QuantTrader.Infrastructure.Messaging;

/// <summary>Azure Service Bus implementation of <see cref="IEventBus"/>.</summary>
public sealed class ServiceBusEventBus : IEventBus, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusEventBus> _logger;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private readonly ConcurrentDictionary<string, ServiceBusProcessor> _processors = new();

    public ServiceBusEventBus(ServiceBusClient client, ILogger<ServiceBusEventBus> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(T @event, string topic, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var sender = _senders.GetOrAdd(topic, t => _client.CreateSender(t));
        var json = JsonSerializer.Serialize(@event, JsonOptions);
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            Subject = typeof(T).Name
        };

        await sender.SendMessageAsync(message, ct).ConfigureAwait(false);
        _logger.LogDebug("Published {EventType} to topic {Topic}", typeof(T).Name, topic);
    }

    public async Task SubscribeAsync<T>(string topic, Func<T, CancellationToken, Task> handler, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        // Use the topic name as the subscription name for simplicity.
        // In production, you may want a dedicated subscription per consumer group.
        var subscriptionName = $"{topic}-sub";
        var processor = _client.CreateProcessor(topic, subscriptionName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4,
            PrefetchCount = 8
        });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var body = args.Message.Body.ToString();
                var @event = JsonSerializer.Deserialize<T>(body, JsonOptions);

                if (@event is not null)
                {
                    await handler(@event, args.CancellationToken).ConfigureAwait(false);
                }

                await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from topic {Topic}", topic);
                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken).ConfigureAwait(false);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Service Bus processor error on topic {Topic}: {Source}", topic, args.ErrorSource);
            return Task.CompletedTask;
        };

        _processors.TryAdd(topic, processor);
        await processor.StartProcessingAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Started subscription on topic {Topic} with subscription {Subscription}", topic, subscriptionName);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var processor in _processors.Values)
        {
            await processor.StopProcessingAsync().ConfigureAwait(false);
            await processor.DisposeAsync().ConfigureAwait(false);
        }

        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
