using QuantTrader.Common.Enums;

namespace QuantTrader.Common.Configuration;

/// <summary>
/// Singleton that tracks and exposes the current trading mode for all services.
/// Supports runtime switching (e.g., automatic fallback to Paper when a circuit opens).
/// </summary>
public interface ITradingModeProvider
{
    /// <summary>The currently active trading mode.</summary>
    TradingMode CurrentMode { get; }

    /// <summary>True when the system is placing real orders on the live exchange.</summary>
    bool IsLive => CurrentMode == TradingMode.Live;

    /// <summary>True when the system is simulating order fills locally (no real orders).</summary>
    bool IsPaper => CurrentMode == TradingMode.Paper;

    /// <summary>
    /// Switches the active trading mode at runtime.
    /// Called by Polly's OnOpened callback to auto-fallback to Paper.
    /// </summary>
    void SetMode(TradingMode mode);
}
