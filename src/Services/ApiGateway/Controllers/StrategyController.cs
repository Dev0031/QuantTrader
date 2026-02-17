using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Common.Configuration;
using QuantTrader.Infrastructure.Database;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Provides endpoints for viewing and managing trading strategies.</summary>
[ApiController]
[Route("api/strategies")]
public sealed class StrategyController : ControllerBase
{
    private readonly TradingDbContext _db;
    private readonly IOptionsMonitor<StrategySettings> _strategySettings;
    private readonly ILogger<StrategyController> _logger;

    public StrategyController(
        TradingDbContext db,
        IOptionsMonitor<StrategySettings> strategySettings,
        ILogger<StrategyController> logger)
    {
        _db = db;
        _strategySettings = strategySettings;
        _logger = logger;
    }

    /// <summary>Lists all strategies with their current enabled/disabled status.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllStrategies(CancellationToken ct)
    {
        var settings = _strategySettings.CurrentValue;
        var strategies = await _db.Trades
            .GroupBy(t => t.Strategy)
            .Select(g => new
            {
                Name = g.Key,
                TotalTrades = g.Count(),
                WinningTrades = g.Count(t => t.RealizedPnl > 0),
                TotalPnl = g.Sum(t => t.RealizedPnl),
                LastTradeAt = g.Max(t => (DateTimeOffset?)t.EntryTime)
            })
            .ToListAsync(ct);

        var result = strategies.Select(s =>
        {
            var winRate = s.TotalTrades > 0 ? (double)s.WinningTrades / s.TotalTrades * 100 : 0;
            return new StrategyStatusResponse(
                s.Name,
                Enabled: settings.EnabledStrategies.Contains(s.Name, StringComparer.OrdinalIgnoreCase),
                s.TotalTrades,
                s.WinningTrades,
                s.TotalPnl,
                Math.Round(winRate, 2),
                SharpeRatio: 0, // Computed by strategy engine; placeholder here
                s.LastTradeAt);
        });

        return Ok(result);
    }

    /// <summary>Toggles a strategy's enabled/disabled state.</summary>
    [HttpPut("{name}/toggle")]
    [AllowAnonymous]
    public IActionResult ToggleStrategy(string name)
    {
        var settings = _strategySettings.CurrentValue;
        var isEnabled = settings.EnabledStrategies.Contains(name, StringComparer.OrdinalIgnoreCase);

        if (isEnabled)
        {
            settings.EnabledStrategies.RemoveAll(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
            _logger.LogWarning("Strategy {Strategy} disabled by {User}", name, User.Identity?.Name ?? "unknown");
        }
        else
        {
            settings.EnabledStrategies.Add(name);
            _logger.LogInformation("Strategy {Strategy} enabled by {User}", name, User.Identity?.Name ?? "unknown");
        }

        return Ok(new { name, enabled = !isEnabled });
    }

    /// <summary>Gets performance metrics for a specific strategy.</summary>
    [HttpGet("{name}/performance")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStrategyPerformance(string name, CancellationToken ct)
    {
        var trades = await _db.Trades
            .Where(t => t.Strategy == name && t.Status == "Closed")
            .ToListAsync(ct);

        if (trades.Count == 0)
        {
            return NotFound(new { message = $"No trades found for strategy '{name}'" });
        }

        var winningTrades = trades.Count(t => t.RealizedPnl > 0);
        var winRate = (double)winningTrades / trades.Count * 100;
        var totalPnl = trades.Sum(t => t.RealizedPnl);
        var avgWin = winningTrades > 0
            ? trades.Where(t => t.RealizedPnl > 0).Average(t => t.RealizedPnl)
            : 0m;
        var losingCount = trades.Count(t => t.RealizedPnl <= 0);
        var avgLoss = losingCount > 0
            ? trades.Where(t => t.RealizedPnl <= 0).Average(t => t.RealizedPnl)
            : 0m;

        var settings = _strategySettings.CurrentValue;

        return Ok(new StrategyStatusResponse(
            name,
            Enabled: settings.EnabledStrategies.Contains(name, StringComparer.OrdinalIgnoreCase),
            trades.Count,
            winningTrades,
            totalPnl,
            Math.Round(winRate, 2),
            SharpeRatio: 0, // Placeholder; computed by the strategy engine
            trades.Max(t => (DateTimeOffset?)t.ExitTime)));
    }
}
