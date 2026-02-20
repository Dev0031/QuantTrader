using QuantTrader.Common.Enums;
using QuantTrader.ExecutionEngine.Models;

namespace QuantTrader.ExecutionEngine.Adapters;

/// <summary>
/// Unified port for order execution. Live and Paper modes implement this interface identically —
/// the only difference is whether real money is involved.
/// OrderExecutor injects this instead of IBinanceTradeClient directly.
/// </summary>
public interface IOrderAdapter
{
    /// <summary>Human-readable name of the adapter (e.g., "Live", "Paper").</summary>
    string Name { get; }

    Task<OrderResult> PlaceMarketOrderAsync(string symbol, OrderSide side, decimal quantity, CancellationToken ct = default);
    Task<OrderResult> PlaceLimitOrderAsync(string symbol, OrderSide side, decimal quantity, decimal price, CancellationToken ct = default);
    Task<OrderResult> PlaceStopLossOrderAsync(string symbol, OrderSide side, decimal quantity, decimal stopPrice, CancellationToken ct = default);
    Task<OrderResult> CancelOrderAsync(string orderId, string symbol, CancellationToken ct = default);
    Task<OrderResult> QueryOrderAsync(string orderId, string symbol, CancellationToken ct = default);
    Task<Dictionary<string, decimal>> GetAccountBalanceAsync(CancellationToken ct = default);
}
