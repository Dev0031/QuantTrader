using QuantTrader.Common.Models;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>Tracks open trading positions with PnL calculations. Persists to Redis and DB.</summary>
public interface IPositionTracker
{
    /// <summary>Opens a new position from a filled order.</summary>
    Task OpenPositionAsync(Order filledOrder, CancellationToken ct = default);

    /// <summary>Closes or partially closes a position at the given exit price.</summary>
    Task ClosePositionAsync(string symbol, decimal exitPrice, decimal quantity, CancellationToken ct = default);

    /// <summary>Returns all currently open positions.</summary>
    Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default);

    /// <summary>Returns the open position for a specific symbol, or null if none exists.</summary>
    Task<Position?> GetPositionAsync(string symbol, CancellationToken ct = default);

    /// <summary>Updates unrealized PnL for a position based on current market price.</summary>
    Task UpdateUnrealizedPnlAsync(string symbol, decimal currentPrice, CancellationToken ct = default);
}
