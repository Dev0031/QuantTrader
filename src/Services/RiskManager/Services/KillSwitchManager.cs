using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Common.Services;
using QuantTrader.Infrastructure.Messaging;

namespace QuantTrader.RiskManager.Services;

/// <summary>Manages the kill switch that halts all trading when critical risk thresholds are breached.</summary>
public sealed class KillSwitchManager : IKillSwitchManager
{
    private readonly IEventBus _eventBus;
    private readonly IDrawdownMonitor _drawdownMonitor;
    private readonly RiskSettings _settings;
    private readonly ITimeProvider _time;
    private readonly ILogger<KillSwitchManager> _logger;

    private volatile bool _isActive;
    private readonly List<decimal> _recentLosses = new();
    private readonly object _lock = new();

    public KillSwitchManager(
        IEventBus eventBus,
        IDrawdownMonitor drawdownMonitor,
        IOptions<RiskSettings> settings,
        ITimeProvider time,
        ILogger<KillSwitchManager> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _drawdownMonitor = drawdownMonitor ?? throw new ArgumentNullException(nameof(drawdownMonitor));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsActive => _isActive;

    public async Task ActivateAsync(string reason, CancellationToken ct = default)
    {
        if (_isActive) return;

        _isActive = true;

        _logger.LogCritical("KILL SWITCH ACTIVATED: {Reason}", reason);

        var killEvent = new KillSwitchTriggeredEvent(
            Reason: reason,
            DrawdownPercent: _drawdownMonitor.CurrentDrawdownPercent,
            CorrelationId: Guid.NewGuid().ToString(),
            Timestamp: _time.UtcNow,
            Source: nameof(KillSwitchManager));

        await _eventBus.PublishAsync(killEvent, EventTopics.KillSwitch, ct).ConfigureAwait(false);
    }

    public Task DeactivateAsync(CancellationToken ct = default)
    {
        _isActive = false;
        lock (_lock)
        {
            _recentLosses.Clear();
        }

        _logger.LogWarning("Kill switch deactivated by manual reset");
        return Task.CompletedTask;
    }

    public async Task CheckConditionsAsync(PortfolioSnapshot portfolio, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(portfolio);

        if (!_settings.KillSwitchEnabled || _isActive) return;

        // Condition 1: Max drawdown exceeded
        if (_drawdownMonitor.IsKillSwitchTriggered)
        {
            await ActivateAsync(
                $"Maximum drawdown of {_settings.MaxDrawdownPercent:F2}% exceeded. " +
                $"Current drawdown: {_drawdownMonitor.CurrentDrawdownPercent:F2}%",
                ct).ConfigureAwait(false);
            return;
        }

        // Condition 2: Daily loss limit
        if (_settings.MaxDailyLoss > 0 && portfolio.TotalRealizedPnl < 0)
        {
            var dailyLossPercent = (double)(Math.Abs(portfolio.TotalRealizedPnl) / portfolio.TotalEquity * 100m);
            if (dailyLossPercent >= (double)_settings.MaxDailyLoss)
            {
                await ActivateAsync(
                    $"Daily loss limit of {_settings.MaxDailyLoss}% exceeded. " +
                    $"Current daily loss: {dailyLossPercent:F2}%",
                    ct).ConfigureAwait(false);
                return;
            }
        }

        // Condition 3: Rapid loss detection (3 consecutive losses)
        lock (_lock)
        {
            if (portfolio.TotalRealizedPnl < 0)
            {
                _recentLosses.Add(portfolio.TotalRealizedPnl);
            }
            else
            {
                _recentLosses.Clear();
            }

            if (_recentLosses.Count >= 3)
            {
                _recentLosses.Clear();
                _ = ActivateAsync("Rapid loss detection: 3 consecutive losing snapshots detected.", ct);
            }
        }
    }
}
