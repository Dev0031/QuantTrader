using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Services;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.RiskManager.Services;

namespace RiskManager.Tests;

public class DrawdownMonitorTests
{
    private readonly DrawdownMonitor _monitor;
    private readonly Mock<IRedisCacheService> _cacheMock;
    private readonly RiskSettings _settings;

    public DrawdownMonitorTests()
    {
        _cacheMock = new Mock<IRedisCacheService>();
        _cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _settings = new RiskSettings
        {
            MaxDrawdownPercent = 5.0
        };

        var options = Options.Create(_settings);
        var time = new SystemTimeProvider();
        var logger = Mock.Of<ILogger<DrawdownMonitor>>();
        _monitor = new DrawdownMonitor(_cacheMock.Object, options, time, logger);
    }

    [Fact]
    public async Task Test_Drawdown_CalculatesCorrectly()
    {
        // Arrange - set peak at 10000, then drop to 9500
        await _monitor.UpdateEquityAsync(10_000m);
        await _monitor.UpdateEquityAsync(9_500m);

        // Act
        var drawdown = _monitor.CurrentDrawdownPercent;

        // Assert - (10000 - 9500) / 10000 * 100 = 5%
        drawdown.Should().Be(5.0);
    }

    [Fact]
    public async Task Test_KillSwitch_TriggersAtThreshold()
    {
        // Arrange - MaxDrawdownPercent is 5.0
        await _monitor.UpdateEquityAsync(10_000m);

        // Act - drop to 9400 which is 6% drawdown, exceeding 5% threshold
        await _monitor.UpdateEquityAsync(9_400m);

        // Assert
        _monitor.CurrentDrawdownPercent.Should().BeGreaterOrEqualTo(5.0);
        _monitor.IsKillSwitchTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task Test_Drawdown_PeakUpdatesOnNewHigh()
    {
        // Arrange
        await _monitor.UpdateEquityAsync(10_000m);
        await _monitor.UpdateEquityAsync(9_500m);
        _monitor.CurrentDrawdownPercent.Should().Be(5.0);

        // Act - new high resets peak
        await _monitor.UpdateEquityAsync(11_000m);

        // Assert - drawdown from new peak should be 0%
        _monitor.CurrentDrawdownPercent.Should().Be(0.0);
    }

    [Fact]
    public async Task Test_Reset_ClearsState()
    {
        // Arrange - establish a drawdown
        await _monitor.UpdateEquityAsync(10_000m);
        await _monitor.UpdateEquityAsync(9_500m);
        _monitor.CurrentDrawdownPercent.Should().Be(5.0);

        // Act - reset sets peak to current equity
        await _monitor.ResetAsync();

        // Assert - drawdown should be 0 since peak was set to current
        _monitor.CurrentDrawdownPercent.Should().Be(0.0);
    }
}
