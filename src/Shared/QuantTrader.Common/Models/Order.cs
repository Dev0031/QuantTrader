using QuantTrader.Common.Enums;

namespace QuantTrader.Common.Models;

/// <summary>Represents an exchange order with execution and status details.</summary>
public sealed record Order(
    Guid Id,
    string? ExchangeOrderId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? Price,
    decimal? StopPrice,
    OrderStatus Status,
    decimal FilledQuantity,
    decimal FilledPrice,
    decimal Commission,
    DateTimeOffset Timestamp,
    DateTimeOffset? UpdatedAt);
