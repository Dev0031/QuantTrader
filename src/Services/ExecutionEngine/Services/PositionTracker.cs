using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Database.Entities;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>
/// Tracks open positions in memory and persists to Redis and the database.
/// Calculates PnL correctly for both long and short positions.
/// </summary>
public sealed class PositionTracker : IPositionTracker
{
    private const string RedisKeyPrefix = "position:open:";
    private static readonly TimeSpan RedisExpiry = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, Position> _openPositions = new();
    private readonly TradingDbContext _dbContext;
    private readonly IRedisCacheService _redisCache;
    private readonly ILogger<PositionTracker> _logger;

    public PositionTracker(
        TradingDbContext dbContext,
        IRedisCacheService redisCache,
        ILogger<PositionTracker> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OpenPositionAsync(Order filledOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filledOrder);

        var side = filledOrder.Side == OrderSide.Buy ? PositionSide.Long : PositionSide.Short;

        var position = new Position(
            Symbol: filledOrder.Symbol,
            Side: side,
            EntryPrice: filledOrder.FilledPrice,
            CurrentPrice: filledOrder.FilledPrice,
            Quantity: filledOrder.FilledQuantity,
            UnrealizedPnl: 0m,
            RealizedPnl: 0m,
            StopLoss: filledOrder.StopPrice,
            TakeProfit: null,
            OpenedAt: DateTimeOffset.UtcNow);

        _openPositions[filledOrder.Symbol] = position;

        // Persist to Redis.
        await _redisCache.SetAsync($"{RedisKeyPrefix}{filledOrder.Symbol}", position, RedisExpiry, ct).ConfigureAwait(false);

        // Persist to DB as a trade entry.
        var tradeEntity = new TradeEntity
        {
            Id = Guid.NewGuid(),
            Symbol = filledOrder.Symbol,
            Side = side.ToString(),
            EntryPrice = filledOrder.FilledPrice,
            Quantity = filledOrder.FilledQuantity,
            RealizedPnl = 0m,
            Commission = filledOrder.Commission,
            Strategy = "ExecutionEngine",
            EntryTime = DateTimeOffset.UtcNow,
            Status = "Open"
        };

        _dbContext.Trades.Add(tradeEntity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Opened {Side} position for {Symbol}: {Quantity} @ {EntryPrice}",
            side, filledOrder.Symbol, filledOrder.FilledQuantity, filledOrder.FilledPrice);
    }

    public async Task ClosePositionAsync(string symbol, decimal exitPrice, decimal quantity, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (!_openPositions.TryGetValue(symbol, out var position))
        {
            _logger.LogWarning("No open position found for {Symbol} to close", symbol);
            return;
        }

        // Calculate realized PnL based on position side.
        var realizedPnl = position.Side == PositionSide.Long
            ? (exitPrice - position.EntryPrice) * quantity
            : (position.EntryPrice - exitPrice) * quantity;

        var remainingQty = position.Quantity - quantity;

        if (remainingQty <= 0)
        {
            // Fully closed.
            _openPositions.TryRemove(symbol, out _);
            _logger.LogInformation(
                "Closed {Side} position for {Symbol}: {Quantity} @ {ExitPrice}, PnL: {PnL}",
                position.Side, symbol, quantity, exitPrice, realizedPnl);
        }
        else
        {
            // Partially closed: update remaining position.
            var updated = position with
            {
                Quantity = remainingQty,
                RealizedPnl = position.RealizedPnl + realizedPnl
            };
            _openPositions[symbol] = updated;
            await _redisCache.SetAsync($"{RedisKeyPrefix}{symbol}", updated, RedisExpiry, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Partially closed {Side} position for {Symbol}: closed {ClosedQty} @ {ExitPrice}, remaining {RemainingQty}, PnL: {PnL}",
                position.Side, symbol, quantity, exitPrice, remainingQty, realizedPnl);
        }

        // Update trade in DB.
        var trade = _dbContext.Trades
            .Where(t => t.Symbol == symbol && t.Status == "Open")
            .OrderByDescending(t => t.EntryTime)
            .FirstOrDefault();

        if (trade is not null)
        {
            trade.ExitPrice = exitPrice;
            trade.ExitTime = DateTimeOffset.UtcNow;
            trade.RealizedPnl = trade.RealizedPnl + realizedPnl;
            trade.Status = remainingQty <= 0 ? "Closed" : "PartialClose";
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        if (remainingQty <= 0)
        {
            // Remove from Redis when fully closed.
            await _redisCache.SetAsync($"{RedisKeyPrefix}{symbol}", position, TimeSpan.FromMinutes(5), ct).ConfigureAwait(false);
        }
    }

    public Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Position> positions = _openPositions.Values.ToList().AsReadOnly();
        return Task.FromResult(positions);
    }

    public Task<Position?> GetPositionAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        _openPositions.TryGetValue(symbol, out var position);
        return Task.FromResult(position);
    }

    public async Task UpdateUnrealizedPnlAsync(string symbol, decimal currentPrice, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (!_openPositions.TryGetValue(symbol, out var position))
        {
            return;
        }

        // Calculate unrealized PnL based on position side.
        var unrealizedPnl = position.Side == PositionSide.Long
            ? (currentPrice - position.EntryPrice) * position.Quantity
            : (position.EntryPrice - currentPrice) * position.Quantity;

        var updated = position with
        {
            CurrentPrice = currentPrice,
            UnrealizedPnl = unrealizedPnl
        };

        _openPositions[symbol] = updated;

        await _redisCache.SetAsync($"{RedisKeyPrefix}{symbol}", updated, RedisExpiry, ct).ConfigureAwait(false);

        _logger.LogDebug(
            "Updated unrealized PnL for {Symbol}: {UnrealizedPnl} (current: {CurrentPrice}, entry: {EntryPrice})",
            symbol, unrealizedPnl, currentPrice, position.EntryPrice);
    }
}
