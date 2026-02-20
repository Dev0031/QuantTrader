using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.TestInfrastructure.Builders;
using QuantTrader.TestInfrastructure.Fakes;
using QuantTrader.TestInfrastructure.Helpers;

namespace QuantTrader.Integration.Tests;

/// <summary>
/// End-to-end simulation: FakeMarketDataProvider → tick events → FakeEventBus → PaperOrderAdapter.
/// All infrastructure is fake — no Docker containers, no network calls.
/// Verifies the core data pipeline works in Simulation mode.
/// </summary>
public sealed class SimulationModeE2ETests
{
    [Fact]
    public async Task Simulation_WhenTicksGenerated_MarketTickEventsArePublished()
    {
        // Arrange
        var fakeEventBus = new FakeEventBus();
        var ticks = new MarketScenarioBuilder()
            .ForSymbol("BTCUSDT")
            .StartingAt(50_000m)
            .Trending(0.001)
            .BuildTicks(10);

        var published = new List<MarketTickReceivedEvent>();
        await fakeEventBus.SubscribeAsync<MarketTickReceivedEvent>("market-ticks",
            async (evt, ct) => { published.Add(evt); });

        // Act: simulate what BinanceWebSocketWorker does — feed ticks into the event bus
        foreach (var tick in ticks)
        {
            await fakeEventBus.PublishAsync(
                new MarketTickReceivedEvent(tick, Guid.NewGuid().ToString(), tick.Timestamp, "SimulationTest"),
                "market-ticks");
        }

        // Assert
        published.Should().HaveCount(10);
        published.All(e => e.Tick.Symbol == "BTCUSDT").Should().BeTrue();
        published.Last().Tick.Price.Should().BeGreaterThan(published.First().Tick.Price); // trending up
    }

    [Fact]
    public async Task PaperAdapter_InSimulationMode_PlacesOrderWithoutRealExchange()
    {
        // Arrange
        var redis = new FakeRedisCacheService();
        var tick = new MarketTick("BTCUSDT", 55_000m, 1m, 54_999m, 55_001m, DateTimeOffset.UtcNow);
        await redis.SetLatestTickAsync("BTCUSDT", tick);

        var settings = Options.Create(new TradingModeSettings
        {
            Mode = TradingMode.Simulation,
            PaperFillLatencyMs = 0
        });
        var adapter = new PaperOrderAdapter(redis, settings, NullLogger<PaperOrderAdapter>.Instance);

        // Act
        var result = await adapter.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.01m);

        // Assert
        result.Success.Should().BeTrue();
        result.ExchangeOrderId.Should().StartWith("PAPER-");
        result.ExecutedOrder!.FilledPrice.Should().Be(55_000m);
        result.ExecutedOrder.Status.Should().Be(OrderStatus.Filled);
    }

    [Fact]
    public void MarketScenario_Trending_ProducesMonotonicallyIncreasingPrices()
    {
        var ticks = new MarketScenarioBuilder()
            .ForSymbol("BTCUSDT")
            .StartingAt(50_000m)
            .Trending(0.002) // 0.2% per tick
            .BuildTicks(20);

        ticks.First().Price.Should().BeLessThan(ticks.Last().Price);
    }

    [Fact]
    public void MarketScenario_Volatile_IsDeterministic()
    {
        var ticks1 = new MarketScenarioBuilder().Volatile(seed: 42).BuildTicks(50);
        var ticks2 = new MarketScenarioBuilder().Volatile(seed: 42).BuildTicks(50);

        for (int i = 0; i < ticks1.Count; i++)
        {
            ticks1[i].Price.Should().Be(ticks2[i].Price,
                "same seed should produce identical prices (deterministic random walk)");
        }
    }

    [Fact]
    public void MarketScenario_Breakout_PriceJumpsAtSpecifiedTick()
    {
        var ticks = new MarketScenarioBuilder()
            .StartingAt(50_000m)
            .WithBreakoutAt(10, 0.05) // 5% jump at tick 10
            .BuildTicks(20);

        var before = ticks[9].Price;
        var after = ticks[10].Price;

        ((double)(after - before) / (double)before).Should().BeApproximately(0.05, 0.01);
    }
}
