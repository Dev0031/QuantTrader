using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.RiskManager.Services;

namespace RiskManager.Tests;

public class RiskEvaluatorTests
{
    private readonly Mock<IPositionSizer> _positionSizerMock;
    private readonly Mock<IDrawdownMonitor> _drawdownMonitorMock;
    private readonly Mock<IKillSwitchManager> _killSwitchMock;
    private readonly Mock<IRedisCacheService> _cacheMock;
    private readonly RiskSettings _settings;
    private readonly RiskEvaluator _evaluator;

    public RiskEvaluatorTests()
    {
        _positionSizerMock = new Mock<IPositionSizer>();
        _drawdownMonitorMock = new Mock<IDrawdownMonitor>();
        _killSwitchMock = new Mock<IKillSwitchManager>();
        _cacheMock = new Mock<IRedisCacheService>();

        _settings = new RiskSettings
        {
            MaxRiskPerTradePercent = 2.0,
            MaxDrawdownPercent = 5.0,
            MinRiskRewardRatio = 2.0,
            MaxOpenPositions = 5
        };

        var options = Options.Create(_settings);
        var logger = Mock.Of<ILogger<RiskEvaluator>>();

        _killSwitchMock.Setup(k => k.IsActive).Returns(false);
        _drawdownMonitorMock.Setup(d => d.IsKillSwitchTriggered).Returns(false);

        _evaluator = new RiskEvaluator(
            _positionSizerMock.Object,
            _drawdownMonitorMock.Object,
            _killSwitchMock.Object,
            _cacheMock.Object,
            options,
            logger);
    }

    private static TradeSignal CreateSignal(
        decimal? price = 50_000m,
        decimal? stopLoss = 49_000m,
        decimal? takeProfit = 53_000m,
        TradeAction action = TradeAction.Buy)
    {
        return new TradeSignal(
            Id: Guid.NewGuid(),
            Symbol: "BTCUSDT",
            Action: action,
            Quantity: 0m,
            Price: price,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            Strategy: "TestStrategy",
            ConfidenceScore: 0.8,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString());
    }

    private PortfolioSnapshot CreatePortfolio(int positionCount = 0)
    {
        var positions = Enumerable.Range(0, positionCount)
            .Select(i => new Position(
                Symbol: $"PAIR{i}",
                Side: PositionSide.Long,
                EntryPrice: 50_000m,
                CurrentPrice: 50_500m,
                Quantity: 0.1m,
                UnrealizedPnl: 50m,
                RealizedPnl: 0m,
                StopLoss: 49_000m,
                TakeProfit: 53_000m,
                OpenedAt: DateTimeOffset.UtcNow))
            .ToList();

        return new PortfolioSnapshot(
            TotalEquity: 10_000m,
            AvailableBalance: 5_000m,
            TotalUnrealizedPnl: 0m,
            TotalRealizedPnl: 0m,
            DrawdownPercent: 0.0,
            Positions: positions,
            Timestamp: DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Test_RejectsSignal_WithoutStopLoss()
    {
        // Arrange
        var signal = CreateSignal(stopLoss: null);

        // Act
        var result = await _evaluator.EvaluateSignalAsync(signal);

        // Assert
        result.Approved.Should().BeFalse();
        result.RejectionReason.Should().Contain("stop-loss");
    }

    [Fact]
    public async Task Test_RejectsSignal_BadRiskReward()
    {
        // Arrange - R:R = (50500 - 50000) / (50000 - 49000) = 500/1000 = 0.5, below 2.0 minimum
        var signal = CreateSignal(
            price: 50_000m,
            stopLoss: 49_000m,
            takeProfit: 50_500m);

        _cacheMock
            .Setup(c => c.GetPortfolioSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePortfolio());

        // Act
        var result = await _evaluator.EvaluateSignalAsync(signal);

        // Assert
        result.Approved.Should().BeFalse();
        result.RejectionReason.Should().Contain("Risk-reward ratio");
    }

    [Fact]
    public async Task Test_RejectsSignal_MaxPositionsReached()
    {
        // Arrange - portfolio already has MaxOpenPositions (5) positions
        var signal = CreateSignal();
        _cacheMock
            .Setup(c => c.GetPortfolioSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePortfolio(positionCount: 5));

        // Act
        var result = await _evaluator.EvaluateSignalAsync(signal);

        // Assert
        result.Approved.Should().BeFalse();
        result.RejectionReason.Should().Contain("Max open positions");
    }

    [Fact]
    public async Task Test_ApprovesSignal_ValidSetup()
    {
        // Arrange - proper signal with good R:R
        // R:R = (53000 - 50000) / (50000 - 49000) = 3000/1000 = 3.0
        var signal = CreateSignal(
            price: 50_000m,
            stopLoss: 49_000m,
            takeProfit: 53_000m);

        _cacheMock
            .Setup(c => c.GetPortfolioSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePortfolio(positionCount: 0));

        _positionSizerMock
            .Setup(p => p.CalculatePositionSize(
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<double>()))
            .Returns(0.2m);

        // Act
        var result = await _evaluator.EvaluateSignalAsync(signal);

        // Assert
        result.Approved.Should().BeTrue();
        result.ApprovedOrder.Should().NotBeNull();
        result.ApprovedOrder!.Symbol.Should().Be("BTCUSDT");
        result.ApprovedOrder.Quantity.Should().Be(0.2m);
        result.RejectionReason.Should().BeNull();
    }

    [Fact]
    public async Task Test_RejectsSignal_KillSwitchActive()
    {
        // Arrange
        _killSwitchMock.Setup(k => k.IsActive).Returns(true);
        var signal = CreateSignal();

        // Act
        var result = await _evaluator.EvaluateSignalAsync(signal);

        // Assert
        result.Approved.Should().BeFalse();
        result.RejectionReason.Should().Contain("Kill switch");
    }
}
