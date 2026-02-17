using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.StrategyEngine.Configuration;
using QuantTrader.StrategyEngine.Strategies;

namespace StrategyEngine.Tests;

public class MaCrossoverTests
{
    private readonly MaCrossoverStrategy _strategy;

    public MaCrossoverTests()
    {
        var logger = Mock.Of<ILogger<MaCrossoverStrategy>>();
        var maCrossoverSettings = Options.Create(new MaCrossoverSettings
        {
            FastPeriod = 5,
            SlowPeriod = 10,
            AtrPeriod = 5,
            AtrStopMultiplier = 2.0m,
            AtrProfitMultiplier = 3.0m
        });
        var strategySettings = Options.Create(new StrategySettings
        {
            EnabledStrategies = new List<string> { "MaCrossover" }
        });

        _strategy = new MaCrossoverStrategy(logger, maCrossoverSettings, strategySettings);
    }

    private static MarketTick CreateTick(decimal price)
    {
        return new MarketTick(
            Symbol: "BTCUSDT",
            Price: price,
            Volume: 1.0m,
            BidPrice: price - 0.5m,
            AskPrice: price + 0.5m,
            Timestamp: DateTimeOffset.UtcNow);
    }

    private static List<Candle> CreateCandles(decimal[] closePrices)
    {
        var baseTime = DateTimeOffset.UtcNow.AddHours(-closePrices.Length);
        return closePrices.Select((close, i) => new Candle(
            Symbol: "BTCUSDT",
            Open: close - 1m,
            High: close + 2m,
            Low: close - 2m,
            Close: close,
            Volume: 100m,
            OpenTime: baseTime.AddHours(i),
            CloseTime: baseTime.AddHours(i + 1),
            Interval: "1h")).ToList();
    }

    [Fact]
    public void Test_BuySignal_OnGoldenCross()
    {
        // Arrange - create candles where fast SMA was below slow, then crosses above
        // Slow SMA needs 10 candles, fast needs 5
        // Start with downtrend (fast below slow), then uptick to cross
        var closePrices = new decimal[]
        {
            100m, 98m, 96m, 94m, 92m,   // declining - slow SMA includes these
            90m, 88m, 86m, 84m, 82m,     // continued decline - slow SMA seed complete
            84m, 86m, 88m, 92m, 98m      // sharp uptick - fast SMA crosses above slow
        };

        var candles = CreateCandles(closePrices);
        var tick = CreateTick(98m);

        // Act
        var signal = _strategy.Evaluate(tick, candles);

        // Assert
        signal.Should().NotBeNull();
        signal!.Action.Should().Be(TradeAction.Buy);
        signal.Symbol.Should().Be("BTCUSDT");
        signal.StopLoss.Should().NotBeNull();
        signal.TakeProfit.Should().NotBeNull();
    }

    [Fact]
    public void Test_SellSignal_OnDeathCross()
    {
        // Arrange - create candles where fast SMA was above slow, then crosses below
        var closePrices = new decimal[]
        {
            82m, 84m, 86m, 88m, 90m,     // rising - slow SMA includes these
            92m, 94m, 96m, 98m, 100m,     // continued rise - slow SMA seed complete
            98m, 96m, 92m, 88m, 82m       // sharp drop - fast SMA crosses below slow
        };

        var candles = CreateCandles(closePrices);
        var tick = CreateTick(82m);

        // Act
        var signal = _strategy.Evaluate(tick, candles);

        // Assert
        signal.Should().NotBeNull();
        signal!.Action.Should().Be(TradeAction.Sell);
        signal.Symbol.Should().Be("BTCUSDT");
    }

    [Fact]
    public void Test_NoSignal_NoCrossover()
    {
        // Arrange - flat price, no crossover occurs
        var closePrices = new decimal[]
        {
            100m, 100m, 100m, 100m, 100m,
            100m, 100m, 100m, 100m, 100m,
            100m, 100m, 100m, 100m, 100m
        };

        var candles = CreateCandles(closePrices);
        var tick = CreateTick(100m);

        // Act
        var signal = _strategy.Evaluate(tick, candles);

        // Assert - no crossover means null signal
        signal.Should().BeNull();
    }
}
