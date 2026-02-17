using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuantTrader.Common.Models;
using StackExchange.Redis;

namespace QuantTrader.Infrastructure.Redis;

/// <summary>Abstraction over Redis caching for market data, portfolio snapshots, and general key-value storage.</summary>
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task<MarketTick?> GetLatestTickAsync(string symbol, CancellationToken ct = default);
    Task SetLatestTickAsync(string symbol, MarketTick tick, CancellationToken ct = default);
    Task<PortfolioSnapshot?> GetPortfolioSnapshotAsync(CancellationToken ct = default);
    Task SetPortfolioSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct = default);
}

/// <summary>Redis-backed implementation of <see cref="IRedisCacheService"/>.</summary>
public sealed class RedisCacheService : IRedisCacheService
{
    private const string TickKeyPrefix = "tick:latest:";
    private const string PortfolioKey = "portfolio:snapshot";
    private static readonly TimeSpan DefaultTickExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPortfolioExpiry = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key).ConfigureAwait(false);

        if (value.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Redis value for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await db.StringSetAsync(key, json, expiry).ConfigureAwait(false);
    }

    public async Task<MarketTick?> GetLatestTickAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return await GetAsync<MarketTick>($"{TickKeyPrefix}{symbol.ToUpperInvariant()}", ct).ConfigureAwait(false);
    }

    public async Task SetLatestTickAsync(string symbol, MarketTick tick, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(tick);
        await SetAsync($"{TickKeyPrefix}{symbol.ToUpperInvariant()}", tick, DefaultTickExpiry, ct).ConfigureAwait(false);
    }

    public async Task<PortfolioSnapshot?> GetPortfolioSnapshotAsync(CancellationToken ct = default)
    {
        return await GetAsync<PortfolioSnapshot>(PortfolioKey, ct).ConfigureAwait(false);
    }

    public async Task SetPortfolioSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await SetAsync(PortfolioKey, snapshot, DefaultPortfolioExpiry, ct).ConfigureAwait(false);
    }
}
