using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides aggregated dashboard endpoints for the trading UI.</summary>
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IRedisCacheService _redis;
    private readonly TradingDbContext _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IRedisCacheService redis,
        TradingDbContext db,
        ILogger<DashboardController> logger)
    {
        _redis = redis;
        _db = db;
        _logger = logger;
    }

    /// <summary>Gets the portfolio overview including today's P&L and active position count.</summary>
    [HttpGet("overview")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        var snapshot = await _redis.GetPortfolioSnapshotAsync(ct);

        var todayStart = DateTimeOffset.UtcNow.Date;
        var todayPnl = await _db.Trades
            .Where(t => t.ExitTime != null && t.ExitTime >= todayStart)
            .SumAsync(t => t.RealizedPnl, ct);

        var response = new PortfolioOverviewResponse(
            TotalEquity: snapshot?.TotalEquity ?? 0m,
            AvailableBalance: snapshot?.AvailableBalance ?? 0m,
            TotalUnrealizedPnl: snapshot?.TotalUnrealizedPnl ?? 0m,
            TotalRealizedPnl: snapshot?.TotalRealizedPnl ?? 0m,
            DrawdownPercent: snapshot?.DrawdownPercent ?? 0,
            ActivePositionCount: snapshot?.Positions.Count ?? 0,
            TodayPnl: todayPnl,
            Timestamp: snapshot?.Timestamp ?? DateTimeOffset.UtcNow);

        return Ok(response);
    }

    /// <summary>Gets recent risk alerts.</summary>
    [HttpGet("alerts")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 200) limit = 50;

        var alerts = await _redis.GetAsync<List<AlertRecord>>("risk:alerts:recent", ct);
        if (alerts is null || alerts.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        return Ok(alerts.Take(limit));
    }

    /// <summary>Gets aggregated system health for all services.</summary>
    [HttpGet("system-health")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSystemHealth(CancellationToken ct)
    {
        var health = await _redis.GetAsync<Dictionary<string, ServiceHealthRecord>>("system:health", ct);
        if (health is null)
        {
            return Ok(new
            {
                status = "Unknown",
                message = "No health data available yet",
                services = Array.Empty<object>()
            });
        }

        var overallHealthy = health.Values.All(h => h.Status == "Healthy");

        return Ok(new
        {
            status = overallHealthy ? "Healthy" : "Degraded",
            services = health
        });
    }

    /// <summary>Internal record for alert data stored in Redis.</summary>
    private sealed record AlertRecord(
        string AlertType,
        string Message,
        string Symbol,
        double Severity,
        DateTimeOffset Timestamp);

    /// <summary>Internal record for per-service health status stored in Redis.</summary>
    private sealed record ServiceHealthRecord(
        string Status,
        DateTimeOffset LastChecked,
        string? Message);
}
