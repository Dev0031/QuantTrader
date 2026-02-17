using StackExchange.Redis;

namespace QuantTrader.DataIngestion.Services;

/// <summary>Redis-backed cache service for storing latest market data.</summary>
public sealed class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    private const string PriceKeyPrefix = "price:latest:";

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task SetLatestPriceAsync(string symbol, decimal price, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{PriceKeyPrefix}{symbol.ToUpperInvariant()}";
            await _db.StringSetAsync(key, price.ToString("F8"), TimeSpan.FromMinutes(5));
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
