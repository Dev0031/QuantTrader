namespace QuantTrader.RiskManager.Services;

/// <summary>Tracks equity drawdown from peak and determines if kill-switch thresholds are breached.</summary>
public interface IDrawdownMonitor
{
    /// <summary>Current drawdown as a percentage from peak equity.</summary>
    double CurrentDrawdownPercent { get; }

    /// <summary>True when the drawdown exceeds the configured maximum threshold.</summary>
    bool IsKillSwitchTriggered { get; }

    /// <summary>Updates the monitor with the latest equity value and persists state.</summary>
    Task UpdateEquityAsync(decimal currentEquity, CancellationToken ct = default);

    /// <summary>Resets peak equity tracking after manual review.</summary>
    Task ResetAsync(CancellationToken ct = default);
}
