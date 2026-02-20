using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;

namespace QuantTrader.TestInfrastructure.Builders;

/// <summary>Fluent builder for test <see cref="TradeSignal"/> instances with sensible defaults.</summary>
public sealed class TradeSignalBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _symbol = "BTCUSDT";
    private TradeAction _action = TradeAction.Buy;
    private decimal? _quantity;
    private decimal? _price = 50_000m;
    private decimal? _stopLoss = 49_000m;
    private decimal? _takeProfit = 52_000m;
    private string _strategy = "TestStrategy";
    private double _confidence = 0.8;

    public TradeSignalBuilder WithSymbol(string symbol) { _symbol = symbol; return this; }
    public TradeSignalBuilder WithAction(TradeAction action) { _action = action; return this; }
    public TradeSignalBuilder WithPrice(decimal price) { _price = price; return this; }
    public TradeSignalBuilder WithStopLoss(decimal stop) { _stopLoss = stop; return this; }
    public TradeSignalBuilder WithTakeProfit(decimal tp) { _takeProfit = tp; return this; }
    public TradeSignalBuilder WithStrategy(string name) { _strategy = name; return this; }
    public TradeSignalBuilder WithConfidence(double confidence) { _confidence = confidence; return this; }
    public TradeSignalBuilder WithNoStopLoss() { _stopLoss = null; return this; }

    public TradeSignal Build() => new TradeSignal(
        Id: _id,
        Symbol: _symbol,
        Action: _action,
        Quantity: _quantity ?? 0m,
        Price: _price,
        StopLoss: _stopLoss,
        TakeProfit: _takeProfit,
        Strategy: _strategy,
        ConfidenceScore: _confidence,
        Timestamp: DateTimeOffset.UtcNow,
        CorrelationId: Guid.NewGuid().ToString());
}
