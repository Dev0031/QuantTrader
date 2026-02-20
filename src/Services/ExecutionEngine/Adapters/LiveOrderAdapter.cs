using Microsoft.Extensions.Logging;
using QuantTrader.Common.Enums;
using QuantTrader.ExecutionEngine.Clients;
using QuantTrader.ExecutionEngine.Models;

namespace QuantTrader.ExecutionEngine.Adapters;

/// <summary>
/// Thin wrapper around <see cref="IBinanceTradeClient"/> that delegates every call to the real Binance REST API.
/// Only active when TradingMode == Live.
/// </summary>
public sealed class LiveOrderAdapter : IOrderAdapter
{
    private readonly IBinanceTradeClient _client;
    private readonly ILogger<LiveOrderAdapter> _logger;

    public string Name => "Live";

    public LiveOrderAdapter(IBinanceTradeClient client, ILogger<LiveOrderAdapter> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<OrderResult> PlaceMarketOrderAsync(string symbol, OrderSide side, decimal quantity, CancellationToken ct = default)
    {
        _logger.LogDebug("Live: PlaceMarketOrder {Side} {Quantity} {Symbol}", side, quantity, symbol);
        return _client.PlaceMarketOrderAsync(symbol, side, quantity, ct);
    }

    public Task<OrderResult> PlaceLimitOrderAsync(string symbol, OrderSide side, decimal quantity, decimal price, CancellationToken ct = default)
    {
        _logger.LogDebug("Live: PlaceLimitOrder {Side} {Quantity} {Symbol} @ {Price}", side, quantity, symbol, price);
        return _client.PlaceLimitOrderAsync(symbol, side, quantity, price, ct);
    }

    public Task<OrderResult> PlaceStopLossOrderAsync(string symbol, OrderSide side, decimal quantity, decimal stopPrice, CancellationToken ct = default)
    {
        _logger.LogDebug("Live: PlaceStopLoss {Side} {Quantity} {Symbol} stop={StopPrice}", side, quantity, symbol, stopPrice);
        return _client.PlaceStopLossOrderAsync(symbol, side, quantity, stopPrice, ct);
    }

    public Task<OrderResult> CancelOrderAsync(string orderId, string symbol, CancellationToken ct = default)
        => _client.CancelOrderAsync(orderId, symbol, ct);

    public Task<OrderResult> QueryOrderAsync(string orderId, string symbol, CancellationToken ct = default)
        => _client.QueryOrderAsync(orderId, symbol, ct);

    public Task<Dictionary<string, decimal>> GetAccountBalanceAsync(CancellationToken ct = default)
        => _client.GetAccountBalanceAsync(ct);
}
