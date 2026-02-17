using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Events;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides endpoints for viewing and managing risk settings and kill switch.</summary>
[ApiController]
[Route("api/risk")]
public sealed class RiskController : ControllerBase
{
    private readonly IRedisCacheService _redis;
    private readonly IOptionsMonitor<RiskSettings> _riskSettings;
    private readonly IEventBus _eventBus;
    private readonly ILogger<RiskController> _logger;

    public RiskController(
        IRedisCacheService redis,
        IOptionsMonitor<RiskSettings> riskSettings,
        IEventBus eventBus,
        ILogger<RiskController> logger)
    {
        _redis = redis;
        _riskSettings = riskSettings;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>Gets the current risk settings.</summary>
    [HttpGet("settings")]
    [AllowAnonymous]
    public IActionResult GetRiskSettings()
    {
        var settings = _riskSettings.CurrentValue;
        return Ok(settings);
    }

    /// <summary>Updates risk settings.</summary>
    [HttpPut("settings")]
    [AllowAnonymous]
    public IActionResult UpdateRiskSettings([FromBody] RiskSettings updated)
    {
        if (updated.MaxRiskPerTradePercent is < 0 or > 100)
        {
            return BadRequest(new { message = "MaxRiskPerTradePercent must be between 0 and 100" });
        }

        if (updated.MaxDrawdownPercent is < 0 or > 100)
        {
            return BadRequest(new { message = "MaxDrawdownPercent must be between 0 and 100" });
        }

        // In a production system this would persist to configuration store.
        // For now, log the update request.
        _logger.LogWarning(
            "Risk settings update requested by {User}: MaxRisk={MaxRisk}%, MaxDD={MaxDD}%, MaxPositions={MaxPos}",
            User.Identity?.Name ?? "unknown",
            updated.MaxRiskPerTradePercent,
            updated.MaxDrawdownPercent,
            updated.MaxOpenPositions);

        return Ok(new { message = "Risk settings update acknowledged", settings = updated });
    }

    /// <summary>Gets current risk metrics including drawdown and exposure.</summary>
    [HttpGet("metrics")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRiskMetrics(CancellationToken ct)
    {
        var snapshot = await _redis.GetPortfolioSnapshotAsync(ct);
        var settings = _riskSettings.CurrentValue;
        var killSwitchActive = await _redis.GetAsync<KillSwitchState>("risk:killswitch", ct);

        var metrics = new RiskMetricsResponse(
            CurrentDrawdownPercent: snapshot?.DrawdownPercent ?? 0,
            MaxDrawdownPercent: settings.MaxDrawdownPercent,
            TotalExposure: snapshot?.Positions.Sum(p => p.Quantity * p.CurrentPrice) ?? 0m,
            OpenPositionCount: snapshot?.Positions.Count ?? 0,
            MaxOpenPositions: settings.MaxOpenPositions,
            DailyLoss: snapshot?.TotalRealizedPnl ?? 0m,
            MaxDailyLoss: settings.MaxDailyLoss,
            KillSwitchActive: killSwitchActive?.IsActive ?? false,
            Timestamp: DateTimeOffset.UtcNow);

        return Ok(metrics);
    }

    /// <summary>Manually activates the kill switch.</summary>
    [HttpPost("killswitch/activate")]
    [AllowAnonymous]
    public async Task<IActionResult> ActivateKillSwitch(CancellationToken ct)
    {
        _logger.LogCritical("Kill switch ACTIVATED manually by {User}", User.Identity?.Name ?? "unknown");

        await _redis.SetAsync("risk:killswitch", new KillSwitchState(true, DateTimeOffset.UtcNow), ct: ct);

        await _eventBus.PublishAsync(
            new KillSwitchTriggeredEvent(
                Reason: "Manual activation via API",
                DrawdownPercent: 0,
                CorrelationId: Guid.NewGuid().ToString(),
                Timestamp: DateTimeOffset.UtcNow,
                Source: "ApiGateway"),
            EventTopics.KillSwitch,
            ct);

        return Ok(new { message = "Kill switch activated", timestamp = DateTimeOffset.UtcNow });
    }

    /// <summary>Deactivates the kill switch.</summary>
    [HttpPost("killswitch/deactivate")]
    [AllowAnonymous]
    public async Task<IActionResult> DeactivateKillSwitch(CancellationToken ct)
    {
        _logger.LogWarning("Kill switch DEACTIVATED by {User}", User.Identity?.Name ?? "unknown");

        await _redis.SetAsync("risk:killswitch", new KillSwitchState(false, DateTimeOffset.UtcNow), ct: ct);

        return Ok(new { message = "Kill switch deactivated", timestamp = DateTimeOffset.UtcNow });
    }

    /// <summary>Internal state record for kill switch tracking in Redis.</summary>
    private sealed record KillSwitchState(bool IsActive, DateTimeOffset UpdatedAt);
}
