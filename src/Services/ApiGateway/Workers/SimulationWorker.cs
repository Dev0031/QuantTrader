using System.Text.Json;
using QuantTrader.ApiGateway.Services;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;
using StackExchange.Redis;

namespace QuantTrader.ApiGateway.Workers;

/// <summary>
/// Optional simulation worker that generates realistic fake market data so the full
/// event → SignalR → dashboard pipeline can be validated without a live Binance connection.
///
/// Activation: set Redis key <c>system:simulation:enabled</c> to "1" (via SystemController).
/// The worker wakes on a 1-second heartbeat and generates ticks for all configured symbols
/// when the flag is set.
/// </summary>
public sealed class SimulationWorker : BackgroundService
{
    private const string EnabledKey = "system:simulation:enabled";
    private const string StatsKey = "system:simulation:stats";
    private const string PriceKeyPrefix = "price:latest:";

    // Realistic seed prices (approximate)
    private static readonly Dictionary<string, decimal> BasePrices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTCUSDT"] = 65_000m,
        ["ETHUSDT"] = 3_200m,
        ["SOLUSDT"] = 95m,
        ["BNBUSDT"] = 380m,
        ["XRPUSDT"] = 0.62m
    };

    private readonly IDatabase _redis;
    private readonly IEventBus _eventBus;
    private readonly IActivityLogService _activity;
    private readonly ILogger<SimulationWorker> _logger;

    private readonly Dictionary<string, decimal> _currentPrices;
    private readonly Random _rng = new();
    private long _ticksGenerated;
    private DateTimeOffset? _startedAt;

    public SimulationWorker(
        IConnectionMultiplexer redis,
        IEventBus eventBus,
        IActivityLogService activity,
        ILogger<SimulationWorker> logger)
    {
        _redis = redis.GetDatabase();
        _eventBus = eventBus;
        _activity = activity;
        _logger = logger;
        _currentPrices = new Dictionary<string, decimal>(BasePrices, StringComparer.OrdinalIgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SimulationWorker ready. Set Redis key '{Key}' = '1' to enable", EnabledKey);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var enabledValue = await _redis.StringGetAsync(EnabledKey);
                var enabled = enabledValue == "1";

                if (enabled)
                {
                    if (_startedAt is null)
                    {
                        _startedAt = DateTimeOffset.UtcNow;
                        _ticksGenerated = 0;
                        await _activity.LogAsync("Simulation", "success",
                            "Simulation mode STARTED - generating synthetic market data", ct: stoppingToken);
                        _logger.LogInformation("SimulationWorker activated");
                    }

                    await EmitTicksAsync(stoppingToken);
                }
                else if (_startedAt is not null)
                {
                    // Was running, now stopped
                    await _activity.LogAsync("Simulation", "info",
                        $"Simulation mode STOPPED after {_ticksGenerated:N0} ticks", ct: stoppingToken);
                    _logger.LogInformation("SimulationWorker deactivated. Ticks generated: {Count}", _ticksGenerated);
                    _startedAt = null;
                    _ticksGenerated = 0;
                }

                await Task.Delay(500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SimulationWorker loop");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task EmitTicksAsync(CancellationToken ct)
    {
        // Pick a random symbol each tick
        var symbols = _currentPrices.Keys.ToArray();
        var symbol = symbols[_rng.Next(symbols.Length)];

        // Random walk: ±0.05% per tick
        var pct = (decimal)((_rng.NextDouble() - 0.5) * 0.001);
        _currentPrices[symbol] = Math.Max(_currentPrices[symbol] * (1 + pct), 0.001m);
        var price = Math.Round(_currentPrices[symbol], 2);

        var volume = Math.Round((decimal)(_rng.NextDouble() * 2 + 0.001), 6);
        var spread = price * 0.0001m;

        var tick = new MarketTick(
            Symbol: symbol,
            Price: price,
            Volume: volume,
            BidPrice: price - spread,
            AskPrice: price + spread,
            Timestamp: DateTimeOffset.UtcNow);

        // Write to Redis so REST endpoints also reflect the simulated price
        await _redis.StringSetAsync(
            $"{PriceKeyPrefix}{symbol}",
            price.ToString("F8"),
            TimeSpan.FromMinutes(5));

        // Publish to Infrastructure event bus → RealTimeNotifier → SignalR
        var tickEvent = new MarketTickReceivedEvent(
            Tick: tick,
            CorrelationId: Guid.NewGuid().ToString("N"),
            Timestamp: DateTimeOffset.UtcNow,
            Source: "Simulation");

        await _eventBus.PublishAsync(tickEvent, EventTopics.MarketTicks, ct);

        _ticksGenerated++;

        // Log every 20th tick to keep the activity feed readable
        if (_ticksGenerated % 20 == 0)
        {
            await _activity.LogAsync(
                "Simulation", "info",
                $"[SIM] {symbol} @ ${price:N2}  |  {_ticksGenerated:N0} ticks generated",
                symbol, ct);
        }

        // Persist stats so the pipeline endpoint can read them
        if (_ticksGenerated % 50 == 0)
        {
            var stats = JsonSerializer.Serialize(new
            {
                enabled = true,
                startedAt = _startedAt,
                ticksGenerated = _ticksGenerated,
                symbols
            });
            await _redis.StringSetAsync(StatsKey, stats, TimeSpan.FromHours(1));
        }
    }
}
