using QuantTrader.Common.Models;

namespace QuantTrader.RiskManager.Services;

/// <summary>Manages the trading kill switch that halts all activity when risk limits are breached.</summary>
public interface IKillSwitchManager
{
    /// <summary>True when the kill switch is currently active and all trading is halted.</summary>
    bool IsActive { get; }

    /// <summary>Activates the kill switch and publishes a KillSwitchTriggeredEvent.</summary>
    Task ActivateAsync(string reason, CancellationToken ct = default);

    /// <summary>Deactivates the kill switch. Manual reset only.</summary>
    Task DeactivateAsync(CancellationToken ct = default);

    /// <summary>Evaluates the portfolio snapshot against kill-switch conditions.</summary>
    Task CheckConditionsAsync(PortfolioSnapshot portfolio, CancellationToken ct = default);
}
