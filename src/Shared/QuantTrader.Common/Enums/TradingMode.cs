namespace QuantTrader.Common.Enums;

/// <summary>
/// Controls which order adapter and market data provider each service uses.
/// Default is Paper — developers must explicitly opt into Live to place real orders.
/// </summary>
public enum TradingMode
{
    /// <summary>Places real orders on the live exchange. Requires valid API credentials.</summary>
    Live,

    /// <summary>Simulates order fills locally. No real orders are ever placed. Safe default.</summary>
    Paper,

    /// <summary>Replays historical data through the strategy pipeline. No live market connection.</summary>
    Backtest,

    /// <summary>Generates synthetic market ticks for end-to-end integration testing.</summary>
    Simulation
}
