using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Database.Entities;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>
/// Tracks pending orders in memory, persists to the database, and updates Redis cache.
/// Removes completed or cancelled orders from active tracking.
/// </summary>
public sealed class OrderTracker : IOrderTracker
{
    private const string RedisKeyPrefix = "order:active:";
    private static readonly TimeSpan RedisExpiry = TimeSpan.FromHours(24);

    private static readonly HashSet<OrderStatus> FinalStatuses = new()
    {
        OrderStatus.Filled,
        OrderStatus.Canceled,
        OrderStatus.Rejected,
        OrderStatus.Expired
    };

    private readonly ConcurrentDictionary<string, Order> _pendingOrders = new();
    private readonly TradingDbContext _dbContext;
    private readonly IRedisCacheService _redisCache;
    private readonly ILogger<OrderTracker> _logger;

    public OrderTracker(
        TradingDbContext dbContext,
        IRedisCacheService redisCache,
        ILogger<OrderTracker> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task TrackAsync(Order order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var key = order.ExchangeOrderId ?? order.Id.ToString();
        _pendingOrders[key] = order;

        // Persist to database.
        var entity = new OrderEntity
        {
            Id = order.Id,
            ExchangeOrderId = order.ExchangeOrderId,
            Symbol = order.Symbol,
            Side = order.Side.ToString(),
            Type = order.Type.ToString(),
            Quantity = order.Quantity,
            Price = order.Price,
            StopPrice = order.StopPrice,
            Status = order.Status.ToString(),
            FilledQuantity = order.FilledQuantity,
            FilledPrice = order.FilledPrice,
            Commission = order.Commission,
            CreatedAt = order.Timestamp,
            UpdatedAt = order.UpdatedAt
        };

        _dbContext.Orders.Add(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        // Cache in Redis.
        await _redisCache.SetAsync($"{RedisKeyPrefix}{key}", order, RedisExpiry, ct).ConfigureAwait(false);

        _logger.LogInformation("Tracking order {Key} for {Symbol} ({Status})", key, order.Symbol, order.Status);
    }

    public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Order> pending = _pendingOrders.Values.ToList().AsReadOnly();
        return Task.FromResult(pending);
    }

    public async Task UpdateStatusAsync(string exchangeOrderId, OrderStatus status, decimal filledQty, decimal filledPrice, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeOrderId);

        _logger.LogInformation(
            "Updating order {ExchangeOrderId}: Status={Status}, FilledQty={FilledQty}, FilledPrice={FilledPrice}",
            exchangeOrderId, status, filledQty, filledPrice);

        // Update in-memory tracking.
        if (_pendingOrders.TryGetValue(exchangeOrderId, out var existing))
        {
            var updated = existing with
            {
                Status = status,
                FilledQuantity = filledQty,
                FilledPrice = filledPrice,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            if (FinalStatuses.Contains(status))
            {
                _pendingOrders.TryRemove(exchangeOrderId, out _);
                _logger.LogInformation("Order {ExchangeOrderId} reached final status {Status}. Removed from active tracking", exchangeOrderId, status);
            }
            else
            {
                _pendingOrders[exchangeOrderId] = updated;
            }

            // Update Redis.
            if (FinalStatuses.Contains(status))
            {
                await _redisCache.SetAsync($"{RedisKeyPrefix}{exchangeOrderId}", updated, TimeSpan.FromMinutes(5), ct).ConfigureAwait(false);
            }
            else
            {
                await _redisCache.SetAsync($"{RedisKeyPrefix}{exchangeOrderId}", updated, RedisExpiry, ct).ConfigureAwait(false);
            }
        }

        // Update database.
        var entity = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.ExchangeOrderId == exchangeOrderId, ct)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            entity.Status = status.ToString();
            entity.FilledQuantity = filledQty;
            entity.FilledPrice = filledPrice;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("Order entity not found in DB for ExchangeOrderId {ExchangeOrderId}", exchangeOrderId);
        }
    }
}
