using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>Tracks pending orders, persists to DB, and maintains Redis cache for active order state.</summary>
public interface IOrderTracker
{
    /// <summary>Adds an order to active tracking.</summary>
    Task TrackAsync(Order order, CancellationToken ct = default);

    /// <summary>Returns all orders that have not reached a final state.</summary>
    Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct = default);

    /// <summary>Updates the status and fill information for a tracked order.</summary>
    Task UpdateStatusAsync(string exchangeOrderId, OrderStatus status, decimal filledQty, decimal filledPrice, CancellationToken ct = default);
}
