using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Redis;
using StackExchange.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides aggregated dashboard endpoints for the trading UI.</summary>
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IRedisCacheService _redis;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly TradingDbContext _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IRedisCacheService redis,
        IConnectionMultiplexer redisConnection,
        TradingDbContext db,
        ILogger<DashboardController> logger)
    {
        _redis = redis;
        _redisConnection = redisConnection;
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

    /// <summary>Gets per-provider integration health status.</summary>
    [HttpGet("integration-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetIntegrationStatus(CancellationToken ct)
    {
        var db = _redisConnection.GetDatabase();
        var statuses = new List<IntegrationStatusResponse>();

        // Check Binance (price:latest keys written by DataIngestion)
        var btcPrice = await db.StringGetAsync("price:latest:BTCUSDT");
        var binanceTtl = await db.KeyTimeToLiveAsync("price:latest:BTCUSDT");
        statuses.Add(new IntegrationStatusResponse(
            Provider: "Binance",
            Status: !btcPrice.IsNullOrEmpty ? "Connected" : "Disconnected",
            LastDataAt: !btcPrice.IsNullOrEmpty ? DateTimeOffset.UtcNow : null,
            LastError: null,
            DataPointsLast5Min: !btcPrice.IsNullOrEmpty ? 1 : 0));

        // Check CoinGecko
        var geckoData = await db.StringGetAsync("coingecko:BTCUSDT");
        statuses.Add(new IntegrationStatusResponse(
            Provider: "CoinGecko",
            Status: !geckoData.IsNullOrEmpty ? "Connected" : "NotConfigured",
            LastDataAt: !geckoData.IsNullOrEmpty ? DateTimeOffset.UtcNow : null,
            LastError: null,
            DataPointsLast5Min: !geckoData.IsNullOrEmpty ? 1 : 0));

        // Check CryptoPanic
        var newsData = await db.StringGetAsync("news:latest");
        statuses.Add(new IntegrationStatusResponse(
            Provider: "CryptoPanic",
            Status: !newsData.IsNullOrEmpty ? "Connected" : "NotConfigured",
            LastDataAt: !newsData.IsNullOrEmpty ? DateTimeOffset.UtcNow : null,
            LastError: null,
            DataPointsLast5Min: !newsData.IsNullOrEmpty ? 1 : 0));

        // Check configured providers from settings
        var settings = await _redis.GetAsync<List<SettingsEntry>>("settings:exchange", ct) ?? [];
        foreach (var entry in settings)
        {
            if (!statuses.Any(s => string.Equals(s.Provider, entry.Exchange, StringComparison.OrdinalIgnoreCase)))
            {
                statuses.Add(new IntegrationStatusResponse(
                    Provider: entry.Exchange,
                    Status: entry.Status == "Verified" ? "Connected" : "Disconnected",
                    LastDataAt: entry.LastVerified,
                    LastError: null,
                    DataPointsLast5Min: 0));
            }
        }

        return Ok(statuses);
    }

    private sealed record AlertRecord(
        string AlertType,
        string Message,
        string Symbol,
        double Severity,
        DateTimeOffset Timestamp);

    private sealed record ServiceHealthRecord(
        string Status,
        DateTimeOffset LastChecked,
        string? Message);

    private sealed record SettingsEntry(
        string Exchange,
        string Status,
        DateTimeOffset? LastVerified);
}
