using QuantTrader.Common.Models;

namespace QuantTrader.TestInfrastructure.Builders;

/// <summary>Fluent builder for test <see cref="PortfolioSnapshot"/> instances with sensible defaults.</summary>
public sealed class PortfolioSnapshotBuilder
{
    private decimal _totalEquity = 10_000m;
    private decimal _availableBalance = 9_500m;
    private decimal _unrealizedPnl = 0m;
    private decimal _realizedPnl = 0m;
    private double _drawdownPercent = 0.0;
    private List<Position> _positions = [];

    public PortfolioSnapshotBuilder WithEquity(decimal equity) { _totalEquity = equity; return this; }
    public PortfolioSnapshotBuilder WithAvailableBalance(decimal balance) { _availableBalance = balance; return this; }
    public PortfolioSnapshotBuilder WithUnrealizedPnl(decimal pnl) { _unrealizedPnl = pnl; return this; }
    public PortfolioSnapshotBuilder WithRealizedPnl(decimal pnl) { _realizedPnl = pnl; return this; }
    public PortfolioSnapshotBuilder WithDrawdown(double pct) { _drawdownPercent = pct; return this; }
    public PortfolioSnapshotBuilder WithPositions(List<Position> positions) { _positions = positions; return this; }
    public PortfolioSnapshotBuilder WithMaxPositions(int count)
    {
        // Fill with dummy positions so MaxOpenPositions checks trigger
        _positions = Enumerable.Range(0, count).Select(i => new Position(
            Symbol: $"SYM{i}USDT",
            Side: Common.Enums.PositionSide.Long,
            EntryPrice: 100m,
            CurrentPrice: 100m,
            Quantity: 1m,
            UnrealizedPnl: 0m,
            RealizedPnl: 0m,
            StopLoss: null,
            TakeProfit: null,
            OpenedAt: DateTimeOffset.UtcNow)).ToList();
        return this;
    }

    public PortfolioSnapshot Build() => new PortfolioSnapshot(
        TotalEquity: _totalEquity,
        AvailableBalance: _availableBalance,
        TotalUnrealizedPnl: _unrealizedPnl,
        TotalRealizedPnl: _realizedPnl,
        DrawdownPercent: _drawdownPercent,
        Positions: _positions,
        Timestamp: DateTimeOffset.UtcNow);
}
