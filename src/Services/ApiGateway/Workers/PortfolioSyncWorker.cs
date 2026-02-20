using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuantTrader.ApiGateway.Services;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Redis;
using StackExchange.Redis;

namespace QuantTrader.ApiGateway.Workers;

/// <summary>
/// Periodically builds a portfolio snapshot from Redis price data and open positions,
/// bridging the gap between DataIngestion's price keys and the dashboard's expected format.
/// </summary>
public sealed class PortfolioSyncWorker : BackgroundService
{
    private static readonly string[] TrackedSymbols =
        ["BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT"];

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SnapshotExpiry = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TickExpiry = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly StaleDataFallbackService _fallback;
    private readonly ILogger<PortfolioSyncWorker> _logger;

    public PortfolioSyncWorker(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        StaleDataFallbackService fallback,
        ILogger<PortfolioSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _fallback = fallback;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PortfolioSyncWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BuildAndWriteSnapshotAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "PortfolioSyncWorker cycle failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task BuildAndWriteSnapshotAsync(CancellationToken ct)
    {
        var db = _redis.GetDatabase();

        // 1. Read prices from DataIngestion's keys (price:latest:{SYM} as plain decimal strings)
        var prices = new Dictionary<string, decimal>();
        foreach (var symbol in TrackedSymbols)
        {
            var raw = await db.StringGetAsync($"price:latest:{symbol}");
            if (!raw.IsNullOrEmpty && decimal.TryParse(raw.ToString(), out var price))
            {
                prices[symbol] = price;
            }
        }

        // 2. Bridge: write tick:latest:{SYM} as JSON MarketTick so GetLatestTick works
        foreach (var (symbol, price) in prices)
        {
            var tick = new MarketTick(
                Symbol: symbol,
                Price: price,
                Volume: 0m,
                BidPrice: price,
                AskPrice: price,
                Timestamp: DateTimeOffset.UtcNow);

            var json = JsonSerializer.Serialize(tick, JsonOptions);
            await db.StringSetAsync($"tick:latest:{symbol}", json, TickExpiry);

            // Update fallback service with last-known-good tick
            _fallback.UpdateTick(symbol, tick);
        }

        // 3. Read open positions from DB
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

        var openTrades = await dbContext.Trades
            .Where(t => t.ExitTime == null)
            .ToListAsync(ct);

        var positions = new List<Position>();
        var totalUnrealizedPnl = 0m;

        foreach (var trade in openTrades)
        {
            var currentPrice = prices.GetValueOrDefault(trade.Symbol, trade.EntryPrice);
            var unrealizedPnl = trade.Side == "Buy"
                ? (currentPrice - trade.EntryPrice) * trade.Quantity
                : (trade.EntryPrice - currentPrice) * trade.Quantity;

            totalUnrealizedPnl += unrealizedPnl;

            positions.Add(new Position(
                Symbol: trade.Symbol,
                Side: trade.Side == "Buy" ? PositionSide.Long : PositionSide.Short,
                EntryPrice: trade.EntryPrice,
                CurrentPrice: currentPrice,
                Quantity: trade.Quantity,
                UnrealizedPnl: unrealizedPnl,
                RealizedPnl: trade.RealizedPnl,
                StopLoss: null,
                TakeProfit: null,
                OpenedAt: trade.EntryTime));
        }

        // 4. Compute portfolio totals
        var totalRealizedPnl = await dbContext.Trades
            .Where(t => t.ExitTime != null)
            .SumAsync(t => t.RealizedPnl, ct);

        var baseBalance = 10000m; // Default starting balance
        var totalEquity = baseBalance + totalRealizedPnl + totalUnrealizedPnl;
        var peakEquity = Math.Max(totalEquity, baseBalance);
        var drawdownPercent = peakEquity > 0
            ? (double)((peakEquity - totalEquity) / peakEquity * 100m)
            : 0.0;

        var snapshot = new PortfolioSnapshot(
            TotalEquity: totalEquity,
            AvailableBalance: totalEquity - positions.Sum(p => p.Quantity * p.CurrentPrice),
            TotalUnrealizedPnl: totalUnrealizedPnl,
            TotalRealizedPnl: totalRealizedPnl,
            DrawdownPercent: drawdownPercent,
            Positions: positions,
            Timestamp: DateTimeOffset.UtcNow);

        // 5. Write snapshot to Redis
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        await db.StringSetAsync("portfolio:snapshot", snapshotJson, SnapshotExpiry);

        // Update fallback service with last-known-good snapshot
        _fallback.UpdatePortfolio(snapshot);

        _logger.LogDebug("Portfolio snapshot updated: equity={Equity}, positions={Count}, prices={PriceCount}",
            totalEquity, positions.Count, prices.Count);
    }
}
