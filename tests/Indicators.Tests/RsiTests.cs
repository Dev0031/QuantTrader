using FluentAssertions;
using QuantTrader.Indicators.Oscillators;

namespace Indicators.Tests;

public class RsiTests
{
    [Fact]
    public void Test_RSI_Overbought()
    {
        // Arrange
        var rsi = new RelativeStrengthIndex(14);

        // Act - feed steadily increasing prices to drive RSI above 70
        for (int i = 0; i < 30; i++)
        {
            rsi.Update(100m + i * 10m);
        }

        // Assert
        rsi.IsReady.Should().BeTrue();
        rsi.Value.Should().BeGreaterThan(70.0);
        rsi.IsOverbought.Should().BeTrue();
    }

    [Fact]
    public void Test_RSI_Oversold()
    {
        // Arrange
        var rsi = new RelativeStrengthIndex(14);

        // Act - feed steadily decreasing prices to drive RSI below 30
        for (int i = 0; i < 30; i++)
        {
            rsi.Update(1000m - i * 10m);
        }

        // Assert
        rsi.IsReady.Should().BeTrue();
        rsi.Value.Should().BeLessThan(30.0);
        rsi.IsOversold.Should().BeTrue();
    }

    [Fact]
    public void Test_RSI_RangeValidation()
    {
        // Arrange
        var rsi = new RelativeStrengthIndex(14);
        var random = new Random(42);

        // Act - feed random prices and verify RSI stays in range
        for (int i = 0; i < 100; i++)
        {
            rsi.Update((decimal)(random.NextDouble() * 1000 + 1));

            // Assert - once ready, RSI should always be between 0 and 100
            if (rsi.IsReady)
            {
                rsi.Value.Should().NotBeNull();
                rsi.Value!.Value.Should().BeGreaterOrEqualTo(0.0);
                rsi.Value!.Value.Should().BeLessOrEqualTo(100.0);
            }
        }
    }
}
