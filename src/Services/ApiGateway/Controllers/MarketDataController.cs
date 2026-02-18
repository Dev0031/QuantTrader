using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Redis;
using StackExchange.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides real-time and historical market data endpoints.</summary>
[ApiController]
[Route("api/market")]
public sealed class MarketDataController : ControllerBase
{
    private static readonly string[] DefaultSymbols =
        ["BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT"];

    private readonly IRedisCacheService _redis;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly TradingDbContext _db;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(
        IRedisCacheService redis,
        IConnectionMultiplexer redisConnection,
        TradingDbContext db,
        ILogger<MarketDataController> logger)
    {
        _redis = redis;
        _redisConnection = redisConnection;
        _db = db;
        _logger = logger;
    }

    /// <summary>Gets the latest tick for a symbol from Redis.</summary>
    [HttpGet("ticks/{symbol}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLatestTick(
        string symbol,
        CancellationToken ct)
    {
        var tick = await _redis.GetLatestTickAsync(symbol, ct);
        if (tick is null)
        {
            return NotFound(new { message = $"No tick data found for {symbol}" });
        }

        var response = new TickResponse(
            tick.Symbol,
            tick.Price,
            tick.Volume,
            tick.BidPrice,
            tick.AskPrice,
            tick.Timestamp);

        return Ok(response);
    }

    /// <summary>Gets historical candles for a symbol from the database.</summary>
    [HttpGet("candles/{symbol}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCandles(
        string symbol,
        [FromQuery] string interval = "1h",
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 1000)
        {
            return BadRequest(new { message = "Limit must be between 1 and 1000" });
        }

        var candles = await _db.Candles
            .Where(c => c.Symbol == symbol.ToUpperInvariant() && c.Interval == interval)
            .OrderByDescending(c => c.OpenTime)
            .Take(limit)
            .Select(c => new CandleResponse(
                c.Symbol,
                c.Open,
                c.High,
                c.Low,
                c.Close,
                c.Volume,
                c.OpenTime,
                c.CloseTime,
                c.Interval))
            .ToListAsync(ct);

        return Ok(candles);
    }

    /// <summary>Gets current prices for all tracked symbols by reading price:latest keys directly from Redis.</summary>
    [HttpGet("prices")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllPrices(CancellationToken ct)
    {
        var db = _redisConnection.GetDatabase();
        var prices = new List<object>();

        foreach (var symbol in DefaultSymbols)
        {
            var raw = await db.StringGetAsync($"price:latest:{symbol}");
            if (!raw.IsNullOrEmpty && decimal.TryParse(raw.ToString(), out var price))
            {
                prices.Add(new
                {
                    symbol,
                    price,
                    volume = 0m,
                    bid = price,
                    ask = price,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        // Fallback: if no price:latest keys found, try tick:latest keys (written by PortfolioSyncWorker)
        if (prices.Count == 0)
        {
            foreach (var symbol in DefaultSymbols)
            {
                var tick = await _redis.GetLatestTickAsync(symbol, ct);
                if (tick is not null)
                {
                    prices.Add(new
                    {
                        symbol = tick.Symbol,
                        price = tick.Price,
                        volume = tick.Volume,
                        bid = tick.BidPrice,
                        ask = tick.AskPrice,
                        timestamp = tick.Timestamp
                    });
                }
            }
        }

        return Ok(prices);
    }

    /// <summary>Gets the current orderbook snapshot for a symbol.</summary>
    [HttpGet("orderbook/{symbol}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOrderbook(
        string symbol,
        CancellationToken ct)
    {
        var orderbook = await _redis.GetAsync<object>($"orderbook:{symbol.ToUpperInvariant()}", ct);
        if (orderbook is null)
        {
            return NotFound(new { message = $"No orderbook data found for {symbol}" });
        }

        return Ok(orderbook);
    }
}
