using FluentAssertions;
using QuantTrader.Indicators.MovingAverages;

namespace Indicators.Tests;

public class EmaTests
{
    [Fact]
    public void Test_EMA_CalculatesCorrectly()
    {
        // Arrange
        var ema = new ExponentialMovingAverage(3);
        // Period 3: multiplier = 2/(3+1) = 0.5
        // First 3 values seed the SMA: (10 + 20 + 30) / 3 = 20
        // 4th value: EMA = (40 - 20) * 0.5 + 20 = 30
        // 5th value: EMA = (50 - 30) * 0.5 + 30 = 40
        decimal[] values = [10m, 20m, 30m, 40m, 50m];

        // Act
        foreach (var value in values)
        {
            ema.Update(value);
        }

        // Assert
        ema.IsReady.Should().BeTrue();
        ema.Value.Should().Be(40m);
    }

    [Fact]
    public void Test_EMA_ConvergesToPrice()
    {
        // Arrange
        var ema = new ExponentialMovingAverage(5);
        const decimal constantPrice = 100m;

        // Act - feed constant value many times
        for (int i = 0; i < 50; i++)
        {
            ema.Update(constantPrice);
        }

        // Assert
        ema.IsReady.Should().BeTrue();
        ema.Value.Should().Be(constantPrice);
    }
}
