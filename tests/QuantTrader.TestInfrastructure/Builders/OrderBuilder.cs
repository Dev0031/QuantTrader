using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;

namespace QuantTrader.TestInfrastructure.Builders;

/// <summary>Fluent builder for test <see cref="Order"/> instances with sensible defaults.</summary>
public sealed class OrderBuilder
{
    private Guid _id = Guid.NewGuid();
    private string? _exchangeOrderId;
    private string _symbol = "BTCUSDT";
    private OrderSide _side = OrderSide.Buy;
    private OrderType _type = OrderType.Market;
    private decimal _quantity = 0.01m;
    private decimal? _price;
    private decimal? _stopPrice;
    private OrderStatus _status = OrderStatus.New;

    public OrderBuilder WithId(Guid id) { _id = id; return this; }
    public OrderBuilder WithExchangeOrderId(string id) { _exchangeOrderId = id; return this; }
    public OrderBuilder WithSymbol(string symbol) { _symbol = symbol; return this; }
    public OrderBuilder WithSide(OrderSide side) { _side = side; return this; }
    public OrderBuilder WithType(OrderType type) { _type = type; return this; }
    public OrderBuilder WithQuantity(decimal qty) { _quantity = qty; return this; }
    public OrderBuilder WithPrice(decimal price) { _price = price; return this; }
    public OrderBuilder WithStopPrice(decimal stop) { _stopPrice = stop; return this; }
    public OrderBuilder WithStatus(OrderStatus status) { _status = status; return this; }

    public Order Build() => new Order(
        Id: _id,
        ExchangeOrderId: _exchangeOrderId,
        Symbol: _symbol,
        Side: _side,
        Type: _type,
        Quantity: _quantity,
        Price: _price,
        StopPrice: _stopPrice,
        Status: _status,
        FilledQuantity: _status == OrderStatus.Filled ? _quantity : 0m,
        FilledPrice: _status == OrderStatus.Filled ? (_price ?? 50_000m) : 0m,
        Commission: 0m,
        Timestamp: DateTimeOffset.UtcNow,
        UpdatedAt: null);
}
