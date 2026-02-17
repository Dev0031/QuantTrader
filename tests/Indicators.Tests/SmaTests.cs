using FluentAssertions;
using QuantTrader.Indicators.MovingAverages;

namespace Indicators.Tests;

public class SmaTests
{
    [Fact]
    public void Test_SMA_CalculatesCorrectly()
    {
        // Arrange
        var sma = new SimpleMovingAverage(3);
        decimal[] values = [1m, 2m, 3m, 4m, 5m];

        // Act
        foreach (var value in values)
        {
            sma.Update(value);
        }

        // Assert
        sma.IsReady.Should().BeTrue();
        sma.Value.Should().Be(4.0m); // (3 + 4 + 5) / 3 = 4.0
    }

    [Fact]
    public void Test_SMA_NotReadyBeforePeriod()
    {
        // Arrange
        var sma = new SimpleMovingAverage(5);

        // Act
        sma.Update(1m);
        sma.Update(2m);
        sma.Update(3m);

        // Assert
        sma.IsReady.Should().BeFalse();
        sma.Value.Should().BeNull();
    }

    [Fact]
    public void Test_SMA_Reset()
    {
        // Arrange
        var sma = new SimpleMovingAverage(3);
        sma.Update(1m);
        sma.Update(2m);
        sma.Update(3m);
        sma.IsReady.Should().BeTrue();

        // Act
        sma.Reset();

        // Assert
        sma.IsReady.Should().BeFalse();
        sma.Value.Should().BeNull();
    }
}
