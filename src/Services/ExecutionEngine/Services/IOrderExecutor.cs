using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Models;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>Abstraction for executing, cancelling, and querying orders against the exchange.</summary>
public interface IOrderExecutor
{
    /// <summary>Places an order on the exchange with retry logic.</summary>
    Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default);

    /// <summary>Cancels an existing order on the exchange.</summary>
    Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default);

    /// <summary>Queries the current status of an order on the exchange.</summary>
    Task<OrderResult> GetOrderStatusAsync(string exchangeOrderId, string symbol, CancellationToken ct = default);
}
