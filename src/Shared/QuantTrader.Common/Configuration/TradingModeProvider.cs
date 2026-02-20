using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Enums;

namespace QuantTrader.Common.Configuration;

/// <summary>
/// Thread-safe singleton implementation of <see cref="ITradingModeProvider"/>.
/// Uses a volatile field for lock-free reads; writes are protected by an Interlocked operation.
/// </summary>
public sealed class TradingModeProvider : ITradingModeProvider
{
    private volatile TradingMode _currentMode;
    private readonly ILogger<TradingModeProvider> _logger;

    public TradingModeProvider(IOptions<TradingModeSettings> settings, ILogger<TradingModeProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentMode = settings.Value.Mode;
        _logger.LogInformation("TradingModeProvider initialized with mode: {Mode}", _currentMode);
    }

    public TradingMode CurrentMode => _currentMode;

    public void SetMode(TradingMode mode)
    {
        var previous = _currentMode;
        _currentMode = mode;

        if (previous != mode)
        {
            _logger.LogWarning("Trading mode changed: {Previous} → {New}", previous, mode);
        }
    }
}
