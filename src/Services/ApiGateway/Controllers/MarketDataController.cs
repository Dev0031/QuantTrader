using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides real-time and historical market data endpoints.</summary>
[ApiController]
[Route("api/market")]
public sealed class MarketDataController : ControllerBase
{
    private readonly IRedisCacheService _redis;
    private readonly TradingDbContext _db;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(
        IRedisCacheService redis,
        TradingDbContext db,
        ILogger<MarketDataController> logger)
    {
        _redis = redis;
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

    /// <summary>Gets current prices for all tracked symbols from Redis.</summary>
    [HttpGet("prices")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllPrices(CancellationToken ct)
    {
        var snapshot = await _redis.GetPortfolioSnapshotAsync(ct);
        if (snapshot is null)
        {
            return Ok(Array.Empty<object>());
        }

        var prices = snapshot.Positions.Select(p => new
        {
            p.Symbol,
            Price = p.CurrentPrice,
            Timestamp = snapshot.Timestamp
        });

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
