using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.RiskManager.Services;

namespace RiskManager.Tests;

public class PositionSizerTests
{
    private readonly PositionSizer _sizer;
    private readonly RiskSettings _settings;

    public PositionSizerTests()
    {
        _settings = new RiskSettings
        {
            MaxRiskPerTradePercent = 2.0,
            MaxDrawdownPercent = 5.0
        };

        var options = Options.Create(_settings);
        var logger = Mock.Of<ILogger<PositionSizer>>();
        _sizer = new PositionSizer(options, logger);
    }

    [Fact]
    public void Test_PositionSize_CalculatesCorrectly()
    {
        // Arrange
        decimal equity = 10_000m;
        decimal entryPrice = 50_000m;
        decimal stopLossPrice = 49_000m;
        double riskPercent = 2.0;

        // Act
        // quantity = (10000 * 2 / 100) / |50000 - 49000| = 200 / 1000 = 0.2
        var size = _sizer.CalculatePositionSize(equity, entryPrice, stopLossPrice, riskPercent);

        // Assert
        size.Should().Be(0.2m);
    }

    [Fact]
    public void Test_PositionSize_NeverExceedsMaxRisk()
    {
        // Arrange - request 10% risk but MaxRiskPerTradePercent is 2%
        decimal equity = 10_000m;
        decimal entryPrice = 50_000m;
        decimal stopLossPrice = 49_000m;
        double requestedRisk = 10.0;

        // Act
        var size = _sizer.CalculatePositionSize(equity, entryPrice, stopLossPrice, requestedRisk);

        // Assert - should be capped at 2% risk: (10000 * 2 / 100) / 1000 = 0.2
        var actualRiskAmount = size * Math.Abs(entryPrice - stopLossPrice);
        var actualRiskPercent = (double)(actualRiskAmount / equity * 100m);

        actualRiskPercent.Should().BeLessOrEqualTo(_settings.MaxRiskPerTradePercent);
    }

    [Fact]
    public void Test_PositionSize_ZeroStopDistance_ReturnsZero()
    {
        // Arrange
        decimal equity = 10_000m;
        decimal entryPrice = 50_000m;
        decimal stopLossPrice = 50_000m; // same as entry
        double riskPercent = 2.0;

        // Act
        var size = _sizer.CalculatePositionSize(equity, entryPrice, stopLossPrice, riskPercent);

        // Assert
        size.Should().Be(0m);
    }

    [Fact]
    public void Test_PositionSize_SmallAccount_MinimumSize()
    {
        // Arrange - very small account where calculated size < minimum order size (0.001)
        decimal equity = 1m;
        decimal entryPrice = 50_000m;
        decimal stopLossPrice = 49_000m;
        double riskPercent = 2.0;

        // Act
        // quantity = (1 * 2 / 100) / 1000 = 0.00002, clamped to min 0.001
        var size = _sizer.CalculatePositionSize(equity, entryPrice, stopLossPrice, riskPercent);

        // Assert - should be clamped to minimum order size
        size.Should().Be(0.001m);
    }
}
