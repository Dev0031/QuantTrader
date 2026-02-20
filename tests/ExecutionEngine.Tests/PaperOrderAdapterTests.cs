using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.TestInfrastructure.Builders;
using QuantTrader.TestInfrastructure.Helpers;

namespace QuantTrader.ExecutionEngine.Tests;

public sealed class PaperOrderAdapterTests
{
    private readonly PaperOrderAdapter _adapter;
    private readonly FakeRedisCacheService _redis;

    public PaperOrderAdapterTests()
    {
        _redis = new FakeRedisCacheService();
        var settings = Options.Create(new TradingModeSettings { PaperFillLatencyMs = 0 });
        _adapter = new PaperOrderAdapter(_redis, settings, NullLogger<PaperOrderAdapter>.Instance);
    }

    [Fact]
    public async Task PlaceMarketOrder_ShouldReturnFilled_WithPaperPrefix()
    {
        var result = await _adapter.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.01m);

        result.Success.Should().BeTrue();
        result.ExecutedOrder.Should().NotBeNull();
        result.ExecutedOrder!.Status.Should().Be(OrderStatus.Filled);
        result.ExchangeOrderId.Should().StartWith("PAPER-");
    }

    [Fact]
    public async Task PlaceMarketOrder_WhenRedisHasPrice_ShouldFillAtCachedPrice()
    {
        // Arrange: seed a known price
        var tick = new Common.Models.MarketTick("BTCUSDT", 55_000m, 1m, 54_999m, 55_001m, DateTimeOffset.UtcNow);
        await _redis.SetLatestTickAsync("BTCUSDT", tick);

        // Act
        var result = await _adapter.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.01m);

        result.Success.Should().BeTrue();
        result.ExecutedOrder!.FilledPrice.Should().Be(55_000m);
    }

    [Fact]
    public async Task PlaceMarketOrder_WhenRedisEmpty_ShouldUseFallbackPrice()
    {
        var result = await _adapter.PlaceMarketOrderAsync("NOPRICUSDT", OrderSide.Buy, 1m);

        result.Success.Should().BeTrue();
        result.ExecutedOrder!.FilledPrice.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PlacedOrder_CanBeQueried()
    {
        var placed = await _adapter.PlaceMarketOrderAsync("ETHUSDT", OrderSide.Sell, 0.5m);
        var queried = await _adapter.QueryOrderAsync(placed.ExchangeOrderId!, "ETHUSDT");

        queried.Success.Should().BeTrue();
        queried.ExecutedOrder!.ExchangeOrderId.Should().Be(placed.ExchangeOrderId);
    }

    [Fact]
    public async Task CancelOrder_ShouldUpdateStatus()
    {
        var placed = await _adapter.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.01m);
        var cancelled = await _adapter.CancelOrderAsync(placed.ExchangeOrderId!, "BTCUSDT");

        cancelled.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccountBalance_ShouldReturnPaperBalance()
    {
        var balance = await _adapter.GetAccountBalanceAsync();

        balance.Should().ContainKey("USDT");
        balance["USDT"].Should().Be(10_000m);
    }

    [Fact]
    public void Name_ShouldBePaper()
    {
        _adapter.Name.Should().Be("Paper");
    }
}
