using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;

namespace QuantTrader.ExecutionEngine.Adapters;

/// <summary>
/// Selects the appropriate <see cref="IOrderAdapter"/> based on the current trading mode.
/// Live mode → <see cref="LiveOrderAdapter"/>.
/// Paper, Backtest, Simulation → <see cref="PaperOrderAdapter"/>.
/// </summary>
public sealed class OrderAdapterFactory
{
    private readonly ITradingModeProvider _modeProvider;
    private readonly LiveOrderAdapter _live;
    private readonly PaperOrderAdapter _paper;

    public OrderAdapterFactory(
        ITradingModeProvider modeProvider,
        LiveOrderAdapter live,
        PaperOrderAdapter paper)
    {
        _modeProvider = modeProvider ?? throw new ArgumentNullException(nameof(modeProvider));
        _live = live ?? throw new ArgumentNullException(nameof(live));
        _paper = paper ?? throw new ArgumentNullException(nameof(paper));
    }

    /// <summary>Returns the adapter for the current trading mode.</summary>
    public IOrderAdapter Current => _modeProvider.CurrentMode switch
    {
        TradingMode.Live => _live,
        _ => _paper  // Paper, Backtest, Simulation all use the paper adapter
    };
}
