using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.RiskManager.Models;
using QuantTrader.RiskManager.Services;
using QuantTrader.TestInfrastructure.Builders;
using QuantTrader.TestInfrastructure.Fakes;

namespace QuantTrader.RiskManager.Tests;

public sealed class RiskManagerWorkerComponentTests
{
    [Fact]
    public async Task KillSwitch_WhenActive_EvaluatorRejectsSignal()
    {
        // Arrange
        var killSwitch = new Mock<IKillSwitchManager>();
        killSwitch.Setup(k => k.IsActive).Returns(true);

        var drawdown = new Mock<IDrawdownMonitor>();
        drawdown.Setup(d => d.IsKillSwitchTriggered).Returns(false);
        drawdown.Setup(d => d.CurrentDrawdownPercent).Returns(0);

        var cache = new Mock<Infrastructure.Redis.IRedisCacheService>();
        var settings = Options.Create(new RiskSettings
        {
            MaxRiskPerTradePercent = 2.0,
            MaxDrawdownPercent = 5.0,
            MinRiskRewardRatio = 2.0,
            MaxOpenPositions = 5,
            KillSwitchEnabled = true
        });

        var positionSizer = new PositionSizer(settings, NullLogger<PositionSizer>.Instance);
        var evaluator = new RiskEvaluator(
            positionSizer,
            drawdown.Object,
            killSwitch.Object,
            cache.Object,
            settings,
            NullLogger<RiskEvaluator>.Instance);

        var signal = new TradeSignalBuilder()
            .WithPrice(50_000m)
            .WithStopLoss(49_000m)
            .WithTakeProfit(52_000m)
            .Build();

        // Act
        var result = await evaluator.EvaluateSignalAsync(signal);

        // Assert
        result.Approved.Should().BeFalse();
        result.RejectionReason.Should().Contain("Kill switch");
    }

    [Fact]
    public async Task KillSwitch_WhenDrawdownExceeded_IsActivated()
    {
        // Arrange
        var fakeEventBus = new FakeEventBus();
        var drawdown = new Mock<IDrawdownMonitor>();
        drawdown.Setup(d => d.IsKillSwitchTriggered).Returns(true);
        drawdown.Setup(d => d.CurrentDrawdownPercent).Returns(6.0);

        var settings = Options.Create(new RiskSettings
        {
            MaxDrawdownPercent = 5.0,
            KillSwitchEnabled = true
        });

        var killSwitch = new KillSwitchManager(
            fakeEventBus,
            drawdown.Object,
            settings,
            new Common.Services.FakeTimeProvider(),
            NullLogger<KillSwitchManager>.Instance);

        var portfolio = new PortfolioSnapshotBuilder().Build();

        // Act
        await killSwitch.CheckConditionsAsync(portfolio);

        // Assert
        killSwitch.IsActive.Should().BeTrue();
        fakeEventBus.PublishedEvents<KillSwitchTriggeredEvent>(EventTopics.KillSwitch).Should().HaveCount(1);
    }
}
