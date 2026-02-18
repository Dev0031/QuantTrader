using System.Text.Json;
using StackExchange.Redis;

namespace QuantTrader.DataIngestion.Services;

/// <summary>Redis-backed cache service for storing latest market data.</summary>
public sealed class RedisCacheService : IRedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    private const string PriceKeyPrefix = "price:latest:";

    // Redis pub/sub channel that ApiGateway's RedisTickBridgeWorker subscribes to.
    // Publishing here enables real-time SignalR updates across service boundaries.
    private const string TickChannel = "market:ticks";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task SetLatestPriceAsync(string symbol, decimal price, CancellationToken cancellationToken = default)
    {
        try
        {
            var upperSymbol = symbol.ToUpperInvariant();
            var key = $"{PriceKeyPrefix}{upperSymbol}";
            await _db.StringSetAsync(key, price.ToString("F8"), TimeSpan.FromMinutes(5));

            // Publish to Redis pub/sub so ApiGateway can bridge to SignalR in real-time
            var payload = JsonSerializer.Serialize(new
            {
                symbol = upperSymbol,
                price,
                volume = 0m,
                bidPrice = price * 0.9999m,
                askPrice = price * 1.0001m,
                timestamp = DateTimeOffset.UtcNow
            }, JsonOpts);

            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal(TickChannel), payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set latest price in Redis for {Symbol}", symbol);
        }
    }

    public async Task<decimal?> GetLatestPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{PriceKeyPrefix}{symbol.ToUpperInvariant()}";
            var value = await _db.StringGetAsync(key);
            return value.HasValue && decimal.TryParse(value, out var price) ? price : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get latest price from Redis for {Symbol}", symbol);
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.StringSetAsync(key, value, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set Redis key {Key}", key);
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Redis key {Key}", key);
            return null;
        }
    }
}
