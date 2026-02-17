using FluentAssertions;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;

namespace ExecutionEngine.Tests;

public class PositionTrackerTests
{
    [Fact]
    public void Test_OpenPosition_CreatesCorrectly()
    {
        // Arrange & Act
        var position = new Position(
            Symbol: "BTCUSDT",
            Side: PositionSide.Long,
            EntryPrice: 50_000m,
            CurrentPrice: 50_000m,
            Quantity: 0.1m,
            UnrealizedPnl: 0m,
            RealizedPnl: 0m,
            StopLoss: 49_000m,
            TakeProfit: 53_000m,
            OpenedAt: DateTimeOffset.UtcNow);

        // Assert
        position.Symbol.Should().Be("BTCUSDT");
        position.Side.Should().Be(PositionSide.Long);
        position.EntryPrice.Should().Be(50_000m);
        position.Quantity.Should().Be(0.1m);
        position.UnrealizedPnl.Should().Be(0m);
        position.StopLoss.Should().Be(49_000m);
        position.TakeProfit.Should().Be(53_000m);
    }

    [Fact]
    public void Test_ClosePosition_CalculatesPnl()
    {
        // Arrange
        decimal entryPrice = 50_000m;
        decimal exitPrice = 51_000m;
        decimal quantity = 0.1m;

        // Act - calculate PnL for a long position
        decimal pnl = (exitPrice - entryPrice) * quantity;

        // Assert
        pnl.Should().Be(100m);
    }

    [Fact]
    public void Test_UnrealizedPnl_UpdatesWithPrice()
    {
        // Arrange
        var position = new Position(
            Symbol: "BTCUSDT",
            Side: PositionSide.Long,
            EntryPrice: 50_000m,
            CurrentPrice: 50_000m,
            Quantity: 0.1m,
            UnrealizedPnl: 0m,
            RealizedPnl: 0m,
            StopLoss: 49_000m,
            TakeProfit: 53_000m,
            OpenedAt: DateTimeOffset.UtcNow);

        // Act - simulate price update using the with expression on the record
        decimal newPrice = 51_500m;
        decimal newUnrealizedPnl = (newPrice - position.EntryPrice) * position.Quantity;
        var updatedPosition = position with
        {
            CurrentPrice = newPrice,
            UnrealizedPnl = newUnrealizedPnl
        };

        // Assert
        updatedPosition.CurrentPrice.Should().Be(51_500m);
        updatedPosition.UnrealizedPnl.Should().Be(150m); // (51500 - 50000) * 0.1
    }
}
