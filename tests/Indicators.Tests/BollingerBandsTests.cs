using FluentAssertions;
using QuantTrader.Indicators.Volatility;

namespace Indicators.Tests;

public class BollingerBandsTests
{
    [Fact]
    public void Test_BB_BandsExpandWithVolatility()
    {
        // Arrange
        var bbLowVol = new BollingerBands(period: 5, standardDeviations: 2.0);
        var bbHighVol = new BollingerBands(period: 5, standardDeviations: 2.0);

        // Act - feed low-volatility data to first instance
        decimal[] lowVolData = [100m, 101m, 100m, 101m, 100m];
        foreach (var val in lowVolData)
        {
            bbLowVol.Update(val);
        }

        // Feed high-volatility data to second instance
        decimal[] highVolData = [80m, 120m, 70m, 130m, 90m];
        foreach (var val in highVolData)
        {
            bbHighVol.Update(val);
        }

        // Assert
        bbLowVol.IsReady.Should().BeTrue();
        bbHighVol.IsReady.Should().BeTrue();

        var lowVolWidth = bbLowVol.UpperBand!.Value - bbLowVol.LowerBand!.Value;
        var highVolWidth = bbHighVol.UpperBand!.Value - bbHighVol.LowerBand!.Value;

        highVolWidth.Should().BeGreaterThan(lowVolWidth);
    }

    [Fact]
    public void Test_BB_MiddleBandEqualsSMA()
    {
        // Arrange
        var bb = new BollingerBands(period: 5, standardDeviations: 2.0);
        decimal[] values = [10m, 20m, 30m, 40m, 50m];

        // Act
        foreach (var val in values)
        {
            bb.Update(val);
        }

        // Assert - middle band should equal SMA = (10+20+30+40+50)/5 = 30
        bb.IsReady.Should().BeTrue();
        bb.MiddleBand.Should().Be(30m);
    }

    [Fact]
    public void Test_BB_UpperAboveLower()
    {
        // Arrange
        var bb = new BollingerBands(period: 5, standardDeviations: 2.0);
        decimal[] values = [10m, 20m, 15m, 25m, 18m];

        // Act
        foreach (var val in values)
        {
            bb.Update(val);
        }

        // Assert
        bb.IsReady.Should().BeTrue();
        bb.UpperBand.Should().NotBeNull();
        bb.LowerBand.Should().NotBeNull();
        bb.MiddleBand.Should().NotBeNull();
        bb.UpperBand!.Value.Should().BeGreaterThan(bb.MiddleBand!.Value);
        bb.LowerBand!.Value.Should().BeLessThan(bb.MiddleBand!.Value);
        bb.UpperBand!.Value.Should().BeGreaterThan(bb.LowerBand!.Value);
    }
}
