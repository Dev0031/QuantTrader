using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Services;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.RiskManager.Services;

/// <summary>Tracks equity drawdown from peak and persists state to Redis.</summary>
public sealed class DrawdownMonitor : IDrawdownMonitor
{
    private const string PeakEquityKey = "risk:drawdown:peak-equity";
    private const string CurrentEquityKey = "risk:drawdown:current-equity";

    private readonly IRedisCacheService _cache;
    private readonly RiskSettings _settings;
    private readonly ITimeProvider _time;
    private readonly ILogger<DrawdownMonitor> _logger;

    private decimal _peakEquity;
    private decimal _currentEquity;
    private readonly object _lock = new();

    public DrawdownMonitor(
        IRedisCacheService cache,
        IOptions<RiskSettings> settings,
        ITimeProvider time,
        ILogger<DrawdownMonitor> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public double CurrentDrawdownPercent
    {
        get
        {
            lock (_lock)
            {
                if (_peakEquity <= 0) return 0.0;
                return (double)((_peakEquity - _currentEquity) / _peakEquity * 100m);
            }
        }
    }

    public bool IsKillSwitchTriggered => CurrentDrawdownPercent >= _settings.MaxDrawdownPercent;

    public async Task UpdateEquityAsync(decimal currentEquity, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _currentEquity = currentEquity;

            if (currentEquity > _peakEquity)
            {
                _peakEquity = currentEquity;
            }
        }

        _logger.LogDebug(
            "Drawdown monitor updated: current={Current}, peak={Peak}, drawdown={Drawdown:F2}%",
            currentEquity, _peakEquity, CurrentDrawdownPercent);

        // Persist to Redis
        await _cache.SetAsync(PeakEquityKey, new EquityState(_peakEquity), ct: ct).ConfigureAwait(false);
        await _cache.SetAsync(CurrentEquityKey, new EquityState(currentEquity), ct: ct).ConfigureAwait(false);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _peakEquity = _currentEquity;
        }

        _logger.LogInformation(
            "Drawdown monitor reset. New peak equity set to {PeakEquity}", _peakEquity);

        await _cache.SetAsync(PeakEquityKey, new EquityState(_peakEquity), ct: ct).ConfigureAwait(false);
    }

    /// <summary>Simple wrapper for Redis serialization of a single decimal value.</summary>
    private sealed record EquityState(decimal Value);
}
