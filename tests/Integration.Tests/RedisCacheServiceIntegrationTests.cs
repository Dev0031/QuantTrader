using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.TestInfrastructure.Fixtures;
using StackExchange.Redis;

namespace QuantTrader.Integration.Tests;

[Collection("Redis")]
public sealed class RedisCacheServiceIntegrationTests : IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private IRedisCacheService _cache = null!;

    public RedisCacheServiceIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _cache = new RedisCacheService(_fixture.Multiplexer, NullLogger<RedisCacheService>.Instance);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetAndGetAsync_RoundTrips_Object()
    {
        var tick = new MarketTick("BTCUSDT", 50_000m, 1.5m, 49_999m, 50_001m, DateTimeOffset.UtcNow);

        await _cache.SetAsync("test:tick", tick, TimeSpan.FromMinutes(1));
        var retrieved = await _cache.GetAsync<MarketTick>("test:tick");

        retrieved.Should().NotBeNull();
        retrieved!.Symbol.Should().Be("BTCUSDT");
        retrieved.Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task GetAsync_WhenKeyMissing_ReturnsNull()
    {
        var result = await _cache.GetAsync<MarketTick>("nonexistent:key");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetLatestTick_ThenGet_ReturnsCorrectTick()
    {
        var tick = new MarketTick("ETHUSDT", 3_000m, 10m, 2_999m, 3_001m, DateTimeOffset.UtcNow);

        await _cache.SetLatestTickAsync("ETHUSDT", tick);
        var retrieved = await _cache.GetLatestTickAsync("ETHUSDT");

        retrieved.Should().NotBeNull();
        retrieved!.Price.Should().Be(3_000m);
    }

    [Fact]
    public async Task SetPortfolioSnapshot_ThenGet_ReturnsSnapshot()
    {
        var snapshot = new PortfolioSnapshot(
            TotalEquity: 12_345.67m,
            AvailableBalance: 10_000m,
            TotalUnrealizedPnl: 1_234.56m,
            TotalRealizedPnl: 1_111.11m,
            DrawdownPercent: 1.5,
            Positions: [],
            Timestamp: DateTimeOffset.UtcNow);

        await _cache.SetPortfolioSnapshotAsync(snapshot);
        var retrieved = await _cache.GetPortfolioSnapshotAsync();

        retrieved.Should().NotBeNull();
        retrieved!.TotalEquity.Should().Be(12_345.67m);
    }

    [Fact]
    public async Task SetAsync_WithExpiry_ExpiresCorrectly()
    {
        var tick = new MarketTick("XRPUSDT", 0.5m, 100m, 0.499m, 0.501m, DateTimeOffset.UtcNow);
        await _cache.SetAsync("expire:test", tick, TimeSpan.FromMilliseconds(100));

        // Value should be accessible immediately
        var immediate = await _cache.GetAsync<MarketTick>("expire:test");
        immediate.Should().NotBeNull();

        // Wait for expiry
        await Task.Delay(200);

        var expired = await _cache.GetAsync<MarketTick>("expire:test");
        expired.Should().BeNull();
    }
}
