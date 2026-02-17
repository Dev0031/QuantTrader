using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.StrategyEngine.Services;

namespace StrategyEngine.Tests;

public class CandleAggregatorTests
{
    private readonly CandleAggregator _aggregator;
    private readonly Mock<IEventBus> _eventBusMock;

    public CandleAggregatorTests()
    {
        var logger = Mock.Of<ILogger<CandleAggregator>>();
        _eventBusMock = new Mock<IEventBus>();
        _eventBusMock
            .Setup(e => e.PublishAsync(It.IsAny<CandleClosedEvent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _aggregator = new CandleAggregator(logger, _eventBusMock.Object);
    }

    private static MarketTick CreateTick(decimal price, decimal volume, DateTimeOffset timestamp)
    {
        return new MarketTick(
            Symbol: "BTCUSDT",
            Price: price,
            Volume: volume,
            BidPrice: price - 0.5m,
            AskPrice: price + 0.5m,
            Timestamp: timestamp);
    }

    [Fact]
    public async Task Test_AggregatesTicksIntoCandle()
    {
        // Arrange
        var interval = TimeSpan.FromMinutes(1);
        var baseTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var ticks = new[]
        {
            CreateTick(100m, 1.0m, baseTime),
            CreateTick(105m, 2.0m, baseTime.AddSeconds(15)),
            CreateTick(95m, 1.5m, baseTime.AddSeconds(30)),
            CreateTick(102m, 0.5m, baseTime.AddSeconds(45))
        };

        // Act - feed all ticks within the same 1-minute window
        foreach (var tick in ticks)
        {
            await _aggregator.ProcessTickAsync(tick, interval);
        }

        // Now send a tick in the next window to close the candle
        var closingTick = CreateTick(103m, 1.0m, baseTime.AddMinutes(1).AddSeconds(1));
        await _aggregator.ProcessTickAsync(closingTick, interval);

        // Assert - a CandleClosedEvent should have been published
        _eventBusMock.Verify(
            e => e.PublishAsync(
                It.Is<CandleClosedEvent>(evt =>
                    evt.Candle.Symbol == "BTCUSDT" &&
                    evt.Candle.Open == 100m &&   // first tick price
                    evt.Candle.High == 105m &&   // highest tick price
                    evt.Candle.Low == 95m &&     // lowest tick price
                    evt.Candle.Close == 102m &&  // last tick price in window
                    evt.Candle.Volume == 5.0m),  // sum of volumes: 1+2+1.5+0.5
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Test_EmitsEvent_OnCandleClose()
    {
        // Arrange
        var interval = TimeSpan.FromMinutes(1);
        var baseTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // Act - feed a tick in one window, then one in the next window to trigger close
        await _aggregator.ProcessTickAsync(
            CreateTick(100m, 1.0m, baseTime), interval);

        await _aggregator.ProcessTickAsync(
            CreateTick(200m, 2.0m, baseTime.AddMinutes(1).AddSeconds(1)), interval);

        // Assert - event should have been published exactly once
        _eventBusMock.Verify(
            e => e.PublishAsync(
                It.IsAny<CandleClosedEvent>(),
                "candle.closed",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
