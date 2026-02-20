using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Models;
using QuantTrader.StrategyEngine.Services;
using QuantTrader.StrategyEngine.Strategies;
using QuantTrader.TestInfrastructure.Builders;

namespace QuantTrader.StrategyEngine.Tests;

public sealed class StrategyManagerTests
{
    [Fact]
    public async Task EvaluateAsync_WhenNoStrategiesEnabled_ReturnsEmpty()
    {
        var manager = BuildManager(strategies: []);
        var tick = new MarketScenarioBuilder().BuildTicks(1).First();

        var signals = await manager.EvaluateAsync(tick);

        signals.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhenStrategyProducesSignal_IsIncludedIfConfidenceMeetsThreshold()
    {
        var highConf = CreateStrategy("High", confidence: 0.9);
        var manager = BuildManager([highConf], minConfidence: 0.7);

        // Need at least one candle buffered for evaluation to run
        var tick = new MarketScenarioBuilder().BuildTicks(1).First();
        manager.AppendCandle(new Candle("BTCUSDT",
            tick.Price, tick.Price, tick.Price, tick.Price, 1m,
            tick.Timestamp.AddMinutes(-1), tick.Timestamp, "1m"));

        var signals = await manager.EvaluateAsync(tick);

        signals.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhenConfidenceBelowThreshold_SignalIsFiltered()
    {
        var lowConf = CreateStrategy("Low", confidence: 0.3);
        var manager = BuildManager([lowConf], minConfidence: 0.7);

        var tick = new MarketScenarioBuilder().BuildTicks(1).First();
        manager.AppendCandle(new Candle("BTCUSDT",
            tick.Price, tick.Price, tick.Price, tick.Price, 1m,
            tick.Timestamp.AddMinutes(-1), tick.Timestamp, "1m"));

        var signals = await manager.EvaluateAsync(tick);

        signals.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_MultipleStrategies_AggregatesAllSignals()
    {
        var s1 = CreateStrategy("S1", confidence: 0.8);
        var s2 = CreateStrategy("S2", confidence: 0.9);
        var manager = BuildManager([s1, s2], minConfidence: 0.7);

        var tick = new MarketScenarioBuilder().BuildTicks(1).First();
        manager.AppendCandle(new Candle("BTCUSDT",
            tick.Price, tick.Price, tick.Price, tick.Price, 1m,
            tick.Timestamp.AddMinutes(-1), tick.Timestamp, "1m"));

        var signals = await manager.EvaluateAsync(tick);

        signals.Should().HaveCount(2);
    }

    [Fact]
    public void ResetAll_ShouldNotThrow()
    {
        var manager = BuildManager([]);
        var act = () => manager.ResetAll();
        act.Should().NotThrow();
    }

    private static StrategyManager BuildManager(
        IReadOnlyList<IStrategy> strategies,
        double minConfidence = 0.7)
    {
        var settings = Options.Create(new StrategySettings
        {
            MinConfidenceScore = minConfidence
        });

        // Actual constructor: (ILogger<StrategyManager>, IEnumerable<IStrategy>, IOptions<StrategySettings>)
        return new StrategyManager(
            NullLogger<StrategyManager>.Instance,
            strategies,
            settings);
    }

    private static IStrategy CreateStrategy(string name, double confidence)
    {
        var mock = new Mock<IStrategy>();
        mock.Setup(s => s.Name).Returns(name);
        mock.Setup(s => s.IsEnabled).Returns(true);
        // IStrategy.Evaluate is synchronous: TradeSignal? Evaluate(MarketTick, IReadOnlyList<Candle>)
        mock.Setup(s => s.Evaluate(It.IsAny<MarketTick>(), It.IsAny<IReadOnlyList<Candle>>()))
            .Returns(new TradeSignal(
                Id: Guid.NewGuid(),
                Symbol: "BTCUSDT",
                Action: Common.Enums.TradeAction.Buy,
                Quantity: 0m,
                Price: 50_000m,
                StopLoss: 49_000m,
                TakeProfit: 52_000m,
                Strategy: name,
                ConfidenceScore: confidence,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: Guid.NewGuid().ToString()));
        return mock.Object;
    }
}
