using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Models;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ExecutionEngine.Adapters;

/// <summary>
/// Simulates order execution locally. No real orders are ever placed.
/// Fills are simulated at the latest Redis-cached price with configurable latency.
/// Used when TradingMode is Paper, Backtest, or Simulation.
/// </summary>
public sealed class PaperOrderAdapter : IOrderAdapter
{
    private static readonly Dictionary<string, decimal> DefaultPaperBalance = new()
    {
        ["USDT"] = 10_000m,
        ["BTC"] = 0.5m,
        ["ETH"] = 5m
    };

    private readonly ConcurrentDictionary<string, Order> _paperOrders = new();
    private readonly IRedisCacheService _cache;
    private readonly TradingModeSettings _settings;
    private readonly ILogger<PaperOrderAdapter> _logger;

    public string Name => "Paper";

    public PaperOrderAdapter(
        IRedisCacheService cache,
        IOptions<TradingModeSettings> settings,
        ILogger<PaperOrderAdapter> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyDictionary<string, Order> PlacedOrders => _paperOrders;

    public async Task<OrderResult> PlaceMarketOrderAsync(string symbol, OrderSide side, decimal quantity, CancellationToken ct = default)
    {
        await SimulateFillLatencyAsync(ct);
        var fillPrice = await GetFillPriceAsync(symbol, ct);
        return CreateFilledResult(symbol, side, quantity, fillPrice, OrderType.Market);
    }

    public async Task<OrderResult> PlaceLimitOrderAsync(string symbol, OrderSide side, decimal quantity, decimal price, CancellationToken ct = default)
    {
        await SimulateFillLatencyAsync(ct);
        return CreateFilledResult(symbol, side, quantity, price, OrderType.Limit);
    }

    public async Task<OrderResult> PlaceStopLossOrderAsync(string symbol, OrderSide side, decimal quantity, decimal stopPrice, CancellationToken ct = default)
    {
        await SimulateFillLatencyAsync(ct);
        return CreateFilledResult(symbol, side, quantity, stopPrice, OrderType.StopLoss);
    }

    public Task<OrderResult> CancelOrderAsync(string orderId, string symbol, CancellationToken ct = default)
    {
        if (_paperOrders.TryGetValue(orderId, out var order))
        {
            var cancelled = order with { Status = OrderStatus.Canceled, UpdatedAt = DateTimeOffset.UtcNow };
            _paperOrders[orderId] = cancelled;
            _logger.LogInformation("Paper: Cancelled order {OrderId}", orderId);
            return Task.FromResult(new OrderResult(true, cancelled, null, orderId));
        }
        return Task.FromResult(new OrderResult(false, null, $"Paper order {orderId} not found", null));
    }

    public Task<OrderResult> QueryOrderAsync(string orderId, string symbol, CancellationToken ct = default)
    {
        if (_paperOrders.TryGetValue(orderId, out var order))
            return Task.FromResult(new OrderResult(true, order, null, orderId));

        return Task.FromResult(new OrderResult(false, null, $"Paper order {orderId} not found", null));
    }

    public Task<Dictionary<string, decimal>> GetAccountBalanceAsync(CancellationToken ct = default)
        => Task.FromResult(new Dictionary<string, decimal>(DefaultPaperBalance));

    private async Task<decimal> GetFillPriceAsync(string symbol, CancellationToken ct)
    {
        try
        {
            var tick = await _cache.GetLatestTickAsync(symbol, ct);
            return tick?.Price ?? 50_000m; // Fallback price when Redis has no data
        }
        catch
        {
            _logger.LogWarning("Paper: Could not read fill price from Redis for {Symbol}. Using fallback.", symbol);
            return 50_000m;
        }
    }

    private Task SimulateFillLatencyAsync(CancellationToken ct)
        => _settings.PaperFillLatencyMs > 0
            ? Task.Delay(_settings.PaperFillLatencyMs, ct)
            : Task.CompletedTask;

    private OrderResult CreateFilledResult(string symbol, OrderSide side, decimal quantity, decimal fillPrice, OrderType type)
    {
        var exchangeOrderId = $"PAPER-{Guid.NewGuid():N}";

        var order = new Order(
            Id: Guid.NewGuid(),
            ExchangeOrderId: exchangeOrderId,
            Symbol: symbol,
            Side: side,
            Type: type,
            Quantity: quantity,
            Price: fillPrice,
            StopPrice: null,
            Status: OrderStatus.Filled,
            FilledQuantity: quantity,
            FilledPrice: fillPrice,
            Commission: 0m,
            Timestamp: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        _paperOrders[exchangeOrderId] = order;

        _logger.LogInformation(
            "Paper: {Type} {Side} {Quantity} {Symbol} filled @ {Price} (id={OrderId})",
            type, side, quantity, symbol, fillPrice, exchangeOrderId);

        return new OrderResult(true, order, null, exchangeOrderId);
    }
}
