using QuantTrader.Common.Enums;

namespace QuantTrader.Common.Configuration;

/// <summary>Configuration for the trading mode abstraction layer.</summary>
public sealed class TradingModeSettings
{
    public const string SectionName = "TradingMode";

    /// <summary>Active trading mode. Defaults to Paper (safe).</summary>
    public TradingMode Mode { get; set; } = TradingMode.Paper;

    /// <summary>
    /// When true, the system automatically switches to Paper mode if the Binance circuit breaker opens.
    /// Trading continues safely without interruption.
    /// </summary>
    public bool AutoFallbackToPaperOnCircuitOpen { get; set; } = true;

    /// <summary>Simulated latency for paper order fills in milliseconds.</summary>
    public int PaperFillLatencyMs { get; set; } = 50;
}
