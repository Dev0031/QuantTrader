using System.Collections.Concurrent;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Models;

namespace QuantTrader.TestInfrastructure.Fakes;

/// <summary>
/// In-memory order adapter for unit tests.
/// Records every call in a queue for assertion.
/// Supports failure simulation and configurable delay.
/// </summary>
public sealed class FakeOrderAdapter : IOrderAdapter
{
    private readonly ConcurrentQueue<string> _callLog = new();
    private readonly ConcurrentQueue<OrderResult> _placedResults = new();
    private string? _failureMessage;
    private int _delayMs;

    public string Name => "Fake";

    public IReadOnlyCollection<string> CallLog => _callLog.ToArray();

    /// <summary>Configures the adapter to return a failure result on the next call.</summary>
    public void ConfigureFail(string message) => _failureMessage = message;

    /// <summary>Configures a simulated delay on every call (ms).</summary>
    public void ConfigureDelay(int delayMs) => _delayMs = delayMs;

    /// <summary>Resets all state between tests.</summary>
    public void Reset()
    {
        while (_callLog.TryDequeue(out _)) { }
        while (_placedResults.TryDequeue(out _)) { }
        _failureMessage = null;
        _delayMs = 0;
    }

    public async Task<OrderResult> PlaceMarketOrderAsync(string symbol, OrderSide side, decimal quantity, CancellationToken ct = default)
    {
        _callLog.Enqueue($"PlaceMarket:{symbol}:{side}:{quantity}");
        return await ExecuteAsync(() => CreateSuccess(symbol, side, quantity, null, OrderType.Market), ct);
    }

    public async Task<OrderResult> PlaceLimitOrderAsync(string symbol, OrderSide side, decimal quantity, decimal price, CancellationToken ct = default)
    {
        _callLog.Enqueue($"PlaceLimit:{symbol}:{side}:{quantity}@{price}");
        return await ExecuteAsync(() => CreateSuccess(symbol, side, quantity, price, OrderType.Limit), ct);
    }

    public async Task<OrderResult> PlaceStopLossOrderAsync(string symbol, OrderSide side, decimal quantity, decimal stopPrice, CancellationToken ct = default)
    {
        _callLog.Enqueue($"PlaceStopLoss:{symbol}:{side}:{quantity}stop={stopPrice}");
        return await ExecuteAsync(() => CreateSuccess(symbol, side, quantity, stopPrice, OrderType.StopLoss), ct);
    }

    public async Task<OrderResult> CancelOrderAsync(string orderId, string symbol, CancellationToken ct = default)
    {
        _callLog.Enqueue($"Cancel:{orderId}:{symbol}");
        return await ExecuteAsync(() => new OrderResult(true, null, null, orderId), ct);
    }

    public async Task<OrderResult> QueryOrderAsync(string orderId, string symbol, CancellationToken ct = default)
    {
        _callLog.Enqueue($"Query:{orderId}:{symbol}");
        return await ExecuteAsync(() => new OrderResult(true, null, null, orderId), ct);
    }

    public Task<Dictionary<string, decimal>> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        _callLog.Enqueue("GetAccountBalance");
        return Task.FromResult(new Dictionary<string, decimal> { ["USDT"] = 10_000m });
    }

    private async Task<OrderResult> ExecuteAsync(Func<OrderResult> factory, CancellationToken ct)
    {
        if (_delayMs > 0)
            await Task.Delay(_delayMs, ct);

        if (_failureMessage is not null)
        {
            var msg = _failureMessage;
            _failureMessage = null; // Consume failure (one-shot)
            return new OrderResult(false, null, msg, null);
        }

        return factory();
    }

    private static OrderResult CreateSuccess(string symbol, OrderSide side, decimal quantity, decimal? price, OrderType type)
    {
        var orderId = $"FAKE-{Guid.NewGuid():N}";
        var order = new Order(
            Id: Guid.NewGuid(),
            ExchangeOrderId: orderId,
            Symbol: symbol,
            Side: side,
            Type: type,
            Quantity: quantity,
            Price: price,
            StopPrice: null,
            Status: OrderStatus.Filled,
            FilledQuantity: quantity,
            FilledPrice: price ?? 50_000m,
            Commission: 0m,
            Timestamp: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        return new OrderResult(true, order, null, orderId);
    }
}
