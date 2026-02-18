using System.Text.Json;
using QuantTrader.ApiGateway.Services;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;
using StackExchange.Redis;

namespace QuantTrader.ApiGateway.Workers;

/// <summary>
/// Subscribes to the Redis pub/sub channel that DataIngestion publishes real ticks to,
/// then re-publishes them onto the in-process Infrastructure event bus so RealTimeNotifier
/// can forward them to SignalR clients.
///
/// This bridges the gap between separate microservice processes in local development.
/// In production (Azure Service Bus), this worker is a no-op because all services share
/// the same Service Bus topics.
/// </summary>
public sealed class RedisTickBridgeWorker : BackgroundService
{
    public const string TickChannel = "market:ticks";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly IEventBus _eventBus;
    private readonly IActivityLogService _activity;
    private readonly ILogger<RedisTickBridgeWorker> _logger;

    private long _ticksForwarded;

    public RedisTickBridgeWorker(
        IConnectionMultiplexer redis,
        IEventBus eventBus,
        IActivityLogService activity,
        ILogger<RedisTickBridgeWorker> logger)
    {
        _redis = redis;
        _eventBus = eventBus;
        _activity = activity;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        _logger.LogInformation("RedisTickBridgeWorker subscribing to Redis channel '{Channel}'", TickChannel);

        await subscriber.SubscribeAsync(
            RedisChannel.Literal(TickChannel),
            async (_, message) =>
            {
                if (message.IsNullOrEmpty)
                    return;

                try
                {
                    var payload = JsonSerializer.Deserialize<TickPayload>(message!, JsonOpts);
                    if (payload is null)
                        return;

                    var tick = new MarketTick(
                        Symbol: payload.Symbol,
                        Price: payload.Price,
                        Volume: payload.Volume,
                        BidPrice: payload.BidPrice,
                        AskPrice: payload.AskPrice,
                        Timestamp: payload.Timestamp);

                    var tickEvent = new MarketTickReceivedEvent(
                        Tick: tick,
                        CorrelationId: Guid.NewGuid().ToString("N"),
                        Timestamp: DateTimeOffset.UtcNow,
                        Source: "DataIngestion");

                    await _eventBus.PublishAsync(tickEvent, EventTopics.MarketTicks);

                    _ticksForwarded++;

                    if (_ticksForwarded == 1)
                    {
                        await _activity.LogAsync("DataIngestion", "success",
                            $"Live Binance data flowing. First tick: {payload.Symbol} @ ${payload.Price:N2}",
                            payload.Symbol);
                    }
                    else if (_ticksForwarded % 100 == 0)
                    {
                        await _activity.LogAsync("DataIngestion", "info",
                            $"{payload.Symbol} @ ${payload.Price:N2}  |  {_ticksForwarded:N0} ticks bridged",
                            payload.Symbol);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to bridge Redis tick message");
                }
            });

        await _activity.LogAsync("DataIngestion", "info",
            "Redis pub/sub bridge active. Waiting for DataIngestion to publish ticks...");

        // Hold open until shutdown
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await subscriber.UnsubscribeAsync(RedisChannel.Literal(TickChannel));
        _logger.LogInformation("RedisTickBridgeWorker stopped");
    }

    private sealed record TickPayload(
        string Symbol,
        decimal Price,
        decimal Volume,
        decimal BidPrice,
        decimal AskPrice,
        DateTimeOffset Timestamp);
}
