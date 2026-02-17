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
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string? symbol = null,
        [FromQuery] string? side = null,
        [FromQuery] string? strategyName = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var query = _db.Trades.AsQueryable();

        if (!string.IsNullOrEmpty(startDate) && DateTimeOffset.TryParse(startDate, out var from))
            query = query.Where(t => t.EntryTime >= from);

        if (!string.IsNullOrEmpty(endDate) && DateTimeOffset.TryParse(endDate, out var to))
            query = query.Where(t => t.EntryTime <= to);

        if (!string.IsNullOrEmpty(symbol))
            query = query.Where(t => t.Symbol == symbol.ToUpperInvariant());

        if (!string.IsNullOrEmpty(side))
            query = query.Where(t => t.Side == side);

        if (!string.IsNullOrEmpty(strategyName))
            query = query.Where(t => t.Strategy == strategyName);

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
            items = trades,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>Gets recent trades.</summary>
    [HttpGet("recent")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecentTrades(
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        if (count is < 1 or > 100) count = 10;

        var trades = await _db.Trades
            .OrderByDescending(t => t.EntryTime)
            .Take(count)
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

        return Ok(trades);
    }

    /// <summary>Gets trade statistics/performance summary.</summary>
    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTradeStats(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string? strategyName = null,
        CancellationToken ct = default)
    {
        var query = _db.Trades.Where(t => t.Status == "Closed").AsQueryable();

        if (!string.IsNullOrEmpty(startDate) && DateTimeOffset.TryParse(startDate, out var from))
            query = query.Where(t => t.EntryTime >= from);
        if (!string.IsNullOrEmpty(endDate) && DateTimeOffset.TryParse(endDate, out var to))
            query = query.Where(t => t.EntryTime <= to);
        if (!string.IsNullOrEmpty(strategyName))
            query = query.Where(t => t.Strategy == strategyName);

        var trades = await query.ToListAsync(ct);

        if (trades.Count == 0)
        {
            return Ok(new
            {
                totalTrades = 0,
                winRate = 0.0,
                profitFactor = 0.0,
                averageRR = 0.0,
                totalPnl = 0m,
                averageWin = 0m,
                averageLoss = 0m,
                largestWin = 0m,
                largestLoss = 0m
            });
        }

        var wins = trades.Where(t => t.RealizedPnl > 0).ToList();
        var losses = trades.Where(t => t.RealizedPnl <= 0).ToList();
        var winRate = (double)wins.Count / trades.Count * 100;
        var avgWin = wins.Count > 0 ? wins.Average(t => t.RealizedPnl) : 0m;
        var avgLoss = losses.Count > 0 ? losses.Average(t => t.RealizedPnl) : 0m;
        var profitFactor = avgLoss != 0 ? Math.Abs(avgWin / avgLoss) : 0m;
        var avgRR = avgLoss != 0 ? Math.Abs(avgWin / avgLoss) : 0m;

        return Ok(new
        {
            totalTrades = trades.Count,
            winRate = Math.Round(winRate, 2),
            profitFactor,
            averageRR = avgRR,
            totalPnl = trades.Sum(t => t.RealizedPnl),
            averageWin = avgWin,
            averageLoss = avgLoss,
            largestWin = wins.Count > 0 ? wins.Max(t => t.RealizedPnl) : 0m,
            largestLoss = losses.Count > 0 ? losses.Min(t => t.RealizedPnl) : 0m
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
