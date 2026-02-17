using FluentAssertions;
using QuantTrader.Indicators.Oscillators;

namespace Indicators.Tests;

public class MacdTests
{
    [Fact]
    public void Test_MACD_CrossoverDetection()
    {
        // Arrange
        var macd = new MACD(fastPeriod: 3, slowPeriod: 6, signalPeriod: 3);

        // Act - feed enough data to make it ready, then force a trend change
        // First, feed a flat series to stabilize
        for (int i = 0; i < 20; i++)
        {
            macd.Update(100m);
        }

        // Record histogram sign at stable state
        var histogramBefore = macd.Histogram;

        // Now feed a strong uptrend to push MACD line above signal
        for (int i = 0; i < 10; i++)
        {
            macd.Update(100m + (i + 1) * 5m);
        }

        var histogramAfterUptrend = macd.Histogram;

        // Then feed a strong downtrend to push MACD line below signal
        for (int i = 0; i < 15; i++)
        {
            macd.Update(150m - (i + 1) * 10m);
        }

        var histogramAfterDowntrend = macd.Histogram;

        // Assert - histogram should change sign indicating a crossover
        macd.IsReady.Should().BeTrue();
        histogramAfterUptrend.Should().NotBeNull();
        histogramAfterDowntrend.Should().NotBeNull();

        // Uptrend should produce positive histogram, downtrend should produce negative
        histogramAfterUptrend!.Value.Should().BeGreaterThan(0m);
        histogramAfterDowntrend!.Value.Should().BeLessThan(0m);
    }

    [Fact]
    public void Test_MACD_NotReadyBeforeSlowPeriod()
    {
        // Arrange
        var macd = new MACD(fastPeriod: 12, slowPeriod: 26, signalPeriod: 9);

        // Act - feed fewer values than the slow period
        for (int i = 0; i < 20; i++)
        {
            macd.Update(100m + i);
        }

        // Assert
        macd.IsReady.Should().BeFalse();
        macd.Histogram.Should().BeNull();
    }
}
