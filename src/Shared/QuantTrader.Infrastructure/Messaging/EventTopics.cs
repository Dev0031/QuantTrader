namespace QuantTrader.Infrastructure.Messaging;

/// <summary>Well-known topic name constants for the event bus.</summary>
public static class EventTopics
{
    public const string MarketTicks = "market-ticks";
    public const string CandleClosed = "candle-closed";
    public const string TradeSignals = "trade-signals";
    public const string ApprovedOrders = "approved-orders";
    public const string ExecutedOrders = "executed-orders";
    public const string RiskAlerts = "risk-alerts";
    public const string KillSwitch = "kill-switch";
    public const string SystemHealth = "system-health";
}
