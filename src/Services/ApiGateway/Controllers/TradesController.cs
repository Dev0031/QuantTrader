using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Database;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides trade history and performance summary endpoints.</summary>
[ApiController]
[Route("api/trades")]
public sealed class TradesController : ControllerBase
{
    private readonly TradingDbContext _db;
    private readonly ILogger<TradesController> _logger;

    public TradesController(TradingDbContext db, ILogger<TradesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Gets trade history with pagination and date filters.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var query = _db.Trades.AsQueryable();

        if (from.HasValue)
            query = query.Where(t => t.EntryTime >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.EntryTime <= to.Value);

        var totalCount = await query.CountAsync(ct);

        var trades = await query
            .OrderByDescending(t => t.EntryTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TradeResponse(
                t.Id,
                t.Symbol,
                t.Side,
                t.EntryPrice,
                t.ExitPrice,
                t.Quantity,
                t.RealizedPnl,
                t.Commission,
                t.Strategy,
                t.EntryTime,
                t.ExitTime,
                t.Status))
            .ToListAsync(ct);

        return Ok(new
        {
            data = trades,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>Gets a single trade by ID.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTradeById(Guid id, CancellationToken ct)
    {
        var trade = await _db.Trades
            .Where(t => t.Id == id)
            .Select(t => new TradeResponse(
                t.Id,
                t.Symbol,
                t.Side,
                t.EntryPrice,
                t.ExitPrice,
                t.Quantity,
                t.RealizedPnl,
                t.Commission,
                t.Strategy,
                t.EntryTime,
                t.ExitTime,
                t.Status))
            .FirstOrDefaultAsync(ct);

        if (trade is null)
        {
            return NotFound(new { message = $"Trade {id} not found" });
        }

        return Ok(trade);
    }

    /// <summary>Gets a performance summary of all trades.</summary>
    [HttpGet("summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTradeSummary(CancellationToken ct)
    {
        var closedTrades = await _db.Trades
            .Where(t => t.Status == "Closed")
            .ToListAsync(ct);

        var totalPnl = closedTrades.Sum(t => t.RealizedPnl);
        var totalCommission = closedTrades.Sum(t => t.Commission);
        var winningTrades = closedTrades.Count(t => t.RealizedPnl > 0);
        var losingTrades = closedTrades.Count(t => t.RealizedPnl <= 0);
        var winRate = closedTrades.Count > 0
            ? (double)winningTrades / closedTrades.Count * 100
            : 0;
        var avgWin = winningTrades > 0
            ? closedTrades.Where(t => t.RealizedPnl > 0).Average(t => t.RealizedPnl)
            : 0m;
        var avgLoss = losingTrades > 0
            ? closedTrades.Where(t => t.RealizedPnl <= 0).Average(t => t.RealizedPnl)
            : 0m;
        var profitFactor = avgLoss != 0
            ? Math.Abs(avgWin / avgLoss)
            : 0m;

        return Ok(new
        {
            totalTrades = closedTrades.Count,
            winningTrades,
            losingTrades,
            winRate = Math.Round(winRate, 2),
            totalPnl,
            totalCommission,
            netPnl = totalPnl - totalCommission,
            averageWin = avgWin,
            averageLoss = avgLoss,
            profitFactor
        });
    }

    /// <summary>Gets equity curve data points based on cumulative P&L over time.</summary>
    [HttpGet("equity-curve")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEquityCurve(CancellationToken ct)
    {
        var trades = await _db.Trades
            .Where(t => t.Status == "Closed" && t.ExitTime != null)
            .OrderBy(t => t.ExitTime)
            .Select(t => new { t.ExitTime, t.RealizedPnl })
            .ToListAsync(ct);

        var cumulativePnl = 0m;
        var curve = trades.Select(t =>
        {
            cumulativePnl += t.RealizedPnl;
            return new
            {
                timestamp = t.ExitTime,
                equity = cumulativePnl
            };
        }).ToList();

        return Ok(curve);
    }
}
