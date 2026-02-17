using QuantTrader.Infrastructure.Database.Entities;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>Records and queries trade history in TimescaleDB with performance analytics.</summary>
public interface ITradeJournal
{
    /// <summary>Records a completed trade to the database.</summary>
    Task RecordTradeAsync(TradeEntity trade, CancellationToken ct = default);

    /// <summary>Retrieves trades within the specified time range.</summary>
    Task<IReadOnlyList<TradeEntity>> GetTradeHistoryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>Retrieves all trades for a specific symbol.</summary>
    Task<IReadOnlyList<TradeEntity>> GetTradesBySymbolAsync(string symbol, CancellationToken ct = default);

    /// <summary>Computes performance summary statistics across all recorded trades.</summary>
    Task<PerformanceSummary> GetPerformanceSummaryAsync(CancellationToken ct = default);
}

/// <summary>Aggregated performance statistics across all trades.</summary>
public sealed record PerformanceSummary(
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    decimal WinRate,
    decimal AveragePnl,
    decimal TotalPnl,
    decimal LargestWin,
    decimal LargestLoss,
    decimal AverageWin,
    decimal AverageLoss,
    decimal ProfitFactor);
