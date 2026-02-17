using QuantTrader.Common.Enums;

namespace QuantTrader.Common.Models;

/// <summary>Represents an open trading position with profit/loss tracking.</summary>
public sealed record Position(
    string Symbol,
    PositionSide Side,
    decimal EntryPrice,
    decimal CurrentPrice,
    decimal Quantity,
    decimal UnrealizedPnl,
    decimal RealizedPnl,
    decimal? StopLoss,
    decimal? TakeProfit,
    DateTimeOffset OpenedAt);
