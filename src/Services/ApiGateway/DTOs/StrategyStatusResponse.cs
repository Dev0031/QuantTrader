namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for strategy status and performance.</summary>
public sealed record StrategyStatusResponse(
    string Name,
    bool Enabled,
    int TotalTrades,
    int WinningTrades,
    decimal TotalPnl,
    double WinRate,
    double SharpeRatio,
    DateTimeOffset? LastTradeAt);
