using QuantTrader.Common.Enums;
using QuantTrader.ExecutionEngine.Models;

namespace QuantTrader.ExecutionEngine.Clients;

/// <summary>Abstraction for Binance trade API operations with HMAC-SHA256 signed requests.</summary>
public interface IBinanceTradeClient
{
    /// <summary>Places a market order for the given symbol.</summary>
    Task<OrderResult> PlaceMarketOrderAsync(string symbol, OrderSide side, decimal quantity, CancellationToken ct = default);

    /// <summary>Places a limit order at the specified price.</summary>
    Task<OrderResult> PlaceLimitOrderAsync(string symbol, OrderSide side, decimal quantity, decimal price, CancellationToken ct = default);

    /// <summary>Places a stop-loss order that triggers at the specified stop price.</summary>
    Task<OrderResult> PlaceStopLossOrderAsync(string symbol, OrderSide side, decimal quantity, decimal stopPrice, CancellationToken ct = default);

    /// <summary>Cancels an existing order on Binance.</summary>
    Task<OrderResult> CancelOrderAsync(string orderId, string symbol, CancellationToken ct = default);

    /// <summary>Queries the current status of an order on Binance.</summary>
    Task<OrderResult> QueryOrderAsync(string orderId, string symbol, CancellationToken ct = default);

    /// <summary>Retrieves the current account balances from Binance.</summary>
    Task<Dictionary<string, decimal>> GetAccountBalanceAsync(CancellationToken ct = default);
}
