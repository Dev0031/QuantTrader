using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Database.Entities;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>
/// Persists completed trades to TimescaleDB and provides query and analytics capabilities.
/// </summary>
public sealed class TradeJournal : ITradeJournal
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<TradeJournal> _logger;

    public TradeJournal(TradingDbContext dbContext, ILogger<TradeJournal> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordTradeAsync(TradeEntity trade, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trade);

        _dbContext.Trades.Add(trade);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Recorded trade {TradeId}: {Symbol} {Side} {Quantity} @ {EntryPrice}, PnL: {PnL}",
            trade.Id, trade.Symbol, trade.Side, trade.Quantity, trade.EntryPrice, trade.RealizedPnl);
    }

    public async Task<IReadOnlyList<TradeEntity>> GetTradeHistoryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var trades = await _dbContext.Trades
            .Where(t => t.EntryTime >= from && t.EntryTime <= to)
            .OrderByDescending(t => t.EntryTime)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        _logger.LogDebug("Retrieved {Count} trades between {From} and {To}", trades.Count, from, to);
        return trades.AsReadOnly();
    }

    public async Task<IReadOnlyList<TradeEntity>> GetTradesBySymbolAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var trades = await _dbContext.Trades
            .Where(t => t.Symbol == symbol)
            .OrderByDescending(t => t.EntryTime)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        _logger.LogDebug("Retrieved {Count} trades for {Symbol}", trades.Count, symbol);
        return trades.AsReadOnly();
    }

    public async Task<PerformanceSummary> GetPerformanceSummaryAsync(CancellationToken ct = default)
    {
        var closedTrades = await _dbContext.Trades
            .Where(t => t.Status == "Closed")
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (closedTrades.Count == 0)
        {
            return new PerformanceSummary(
                TotalTrades: 0,
                WinningTrades: 0,
                LosingTrades: 0,
                WinRate: 0m,
                AveragePnl: 0m,
                TotalPnl: 0m,
                LargestWin: 0m,
                LargestLoss: 0m,
                AverageWin: 0m,
                AverageLoss: 0m,
                ProfitFactor: 0m);
        }

        var winners = closedTrades.Where(t => t.RealizedPnl > 0).ToList();
        var losers = closedTrades.Where(t => t.RealizedPnl < 0).ToList();

        var totalPnl = closedTrades.Sum(t => t.RealizedPnl);
        var totalWins = winners.Sum(t => t.RealizedPnl);
        var totalLosses = Math.Abs(losers.Sum(t => t.RealizedPnl));

        var summary = new PerformanceSummary(
            TotalTrades: closedTrades.Count,
            WinningTrades: winners.Count,
            LosingTrades: losers.Count,
            WinRate: closedTrades.Count > 0 ? (decimal)winners.Count / closedTrades.Count * 100m : 0m,
            AveragePnl: closedTrades.Count > 0 ? totalPnl / closedTrades.Count : 0m,
            TotalPnl: totalPnl,
            LargestWin: winners.Count > 0 ? winners.Max(t => t.RealizedPnl) : 0m,
            LargestLoss: losers.Count > 0 ? losers.Min(t => t.RealizedPnl) : 0m,
            AverageWin: winners.Count > 0 ? totalWins / winners.Count : 0m,
            AverageLoss: losers.Count > 0 ? -totalLosses / losers.Count : 0m,
            ProfitFactor: totalLosses > 0 ? totalWins / totalLosses : totalWins > 0 ? decimal.MaxValue : 0m);

        _logger.LogInformation(
            "Performance summary: {TotalTrades} trades, {WinRate:F1}% win rate, total PnL: {TotalPnl}",
            summary.TotalTrades, summary.WinRate, summary.TotalPnl);

        return summary;
    }
}
