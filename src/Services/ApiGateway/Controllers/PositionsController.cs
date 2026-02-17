using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides endpoints for viewing and managing open positions.</summary>
[ApiController]
[Route("api/positions")]
public sealed class PositionsController : ControllerBase
{
    private readonly IRedisCacheService _redis;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PositionsController> _logger;

    public PositionsController(
        IRedisCacheService redis,
        IEventBus eventBus,
        ILogger<PositionsController> logger)
    {
        _redis = redis;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>Gets all open positions from the portfolio snapshot.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllPositions(CancellationToken ct)
    {
        var snapshot = await _redis.GetPortfolioSnapshotAsync(ct);
        if (snapshot is null)
        {
            return Ok(Array.Empty<PositionResponse>());
        }

        var positions = snapshot.Positions.Select(p => new PositionResponse(
            p.Symbol,
            p.Side.ToString(),
            p.EntryPrice,
            p.CurrentPrice,
            p.Quantity,
            p.UnrealizedPnl,
            p.RealizedPnl,
            p.StopLoss,
            p.TakeProfit,
            p.OpenedAt));

        return Ok(positions);
    }

    /// <summary>Gets the open position for a specific symbol.</summary>
    [HttpGet("{symbol}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPositionBySymbol(string symbol, CancellationToken ct)
    {
        var snapshot = await _redis.GetPortfolioSnapshotAsync(ct);
        var position = snapshot?.Positions
            .FirstOrDefault(p => string.Equals(p.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (position is null)
        {
            return NotFound(new { message = $"No open position found for {symbol}" });
        }

        var response = new PositionResponse(
            position.Symbol,
            position.Side.ToString(),
            position.EntryPrice,
            position.CurrentPrice,
            position.Quantity,
            position.UnrealizedPnl,
            position.RealizedPnl,
            position.StopLoss,
            position.TakeProfit,
            position.OpenedAt);

        return Ok(response);
    }

    /// <summary>Manually closes a position for a given symbol. Requires admin role.</summary>
    [HttpPost("{symbol}/close")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ClosePosition(string symbol, CancellationToken ct)
    {
        var snapshot = await _redis.GetPortfolioSnapshotAsync(ct);
        var position = snapshot?.Positions
            .FirstOrDefault(p => string.Equals(p.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (position is null)
        {
            return NotFound(new { message = $"No open position found for {symbol}" });
        }

        _logger.LogInformation("Manual close requested for position {Symbol} by {User}",
            symbol, User.Identity?.Name ?? "unknown");

        await _eventBus.PublishAsync(
            new { Symbol = symbol.ToUpperInvariant(), Reason = "ManualClose", Timestamp = DateTimeOffset.UtcNow },
            "position-close-requests",
            ct);

        return Accepted(new { message = $"Close request submitted for {symbol}" });
    }
}
