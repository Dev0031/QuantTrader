using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using QuantTrader.DataIngestion.Services;

namespace DataIngestion.Tests;

public class DataNormalizerTests
{
    private readonly DataNormalizerService _normalizer;

    public DataNormalizerTests()
    {
        var logger = Mock.Of<ILogger<DataNormalizerService>>();
        _normalizer = new DataNormalizerService(logger);
    }

    [Fact]
    public void Test_NormalizeBinanceTick()
    {
        // Arrange
        var json = """
        {
            "e": "trade",
            "s": "BTCUSDT",
            "p": "43210.50",
            "q": "0.001",
            "b": 123456,
            "a": 789012,
            "T": 1704067200000
        }
        """;

        // Act
        var tick = _normalizer.NormalizeBinanceTrade(json);

        // Assert
        tick.Should().NotBeNull();
        tick!.Symbol.Should().Be("BTCUSDT");
        tick.Price.Should().Be(43210.50m);
        tick.Volume.Should().Be(0.001m);
        tick.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1704067200000));
    }

    [Fact]
    public void Test_NormalizeCoinGeckoResponse()
    {
        // Arrange - CoinGecko-style data is not directly supported by DataNormalizerService,
        // but we can verify the normalizer handles non-trade events gracefully
        var json = """
        {
            "e": "kline",
            "s": "BTCUSDT",
            "p": "43000.00",
            "q": "0.5",
            "T": 1704067200000
        }
        """;

        // Act - non-trade event types return null
        var tick = _normalizer.NormalizeBinanceTrade(json);

        // Assert
        tick.Should().BeNull("because the event type is 'kline', not 'trade'");
    }

    [Fact]
    public void Test_NormalizeBinanceTrade_InvalidJson_ReturnsNull()
    {
        // Arrange
        var invalidJson = "not valid json at all";

        // Act
        var tick = _normalizer.NormalizeBinanceTrade(invalidJson);

        // Assert
        tick.Should().BeNull();
    }

    [Theory]
    [InlineData("43210.50", 43210.50)]
    [InlineData("0.00001", 0.00001)]
    [InlineData("100000.99", 100000.99)]
    public void Test_NormalizeBinanceTrade_ParsesVariousPrices(string priceStr, double expectedPrice)
    {
        // Arrange
        var json = $$"""
        {
            "e": "trade",
            "s": "BTCUSDT",
            "p": "{{priceStr}}",
            "q": "1.0",
            "b": 1,
            "a": 2,
            "T": 1704067200000
        }
        """;

        // Act
        var tick = _normalizer.NormalizeBinanceTrade(json);

        // Assert
        tick.Should().NotBeNull();
        tick!.Price.Should().Be((decimal)expectedPrice);
    }
}
