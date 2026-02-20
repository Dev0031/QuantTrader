using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Clients;
using QuantTrader.ExecutionEngine.Services;
using QuantTrader.TestInfrastructure.Builders;
using QuantTrader.TestInfrastructure.Helpers;

namespace QuantTrader.ExecutionEngine.Tests;

public sealed class OrderExecutorPollyTests
{
    private readonly PaperOrderAdapter _paperAdapter;
    private readonly OrderExecutor _executor;

    public OrderExecutorPollyTests()
    {
        var redis = new FakeRedisCacheService();
        var modeSettings = Options.Create(new TradingModeSettings { PaperFillLatencyMs = 0, Mode = TradingMode.Paper });
        _paperAdapter = new PaperOrderAdapter(redis, modeSettings, NullLogger<PaperOrderAdapter>.Instance);

        var modeProvider = new Mock<ITradingModeProvider>();
        modeProvider.Setup(m => m.CurrentMode).Returns(TradingMode.Paper);

        var live = new LiveOrderAdapter(new Mock<IBinanceTradeClient>().Object, NullLogger<LiveOrderAdapter>.Instance);
        var factory = new OrderAdapterFactory(modeProvider.Object, live, _paperAdapter);

        var execSettings = Options.Create(new ExecutionSettings { MaxRetries = 3, RetryDelayMs = 0 });
        _executor = new OrderExecutor(factory, execSettings, NullLogger<OrderExecutor>.Instance);
    }

    [Fact]
    public async Task PlaceOrder_Market_Succeeds()
    {
        var order = new OrderBuilder().Build();
        var result = await _executor.PlaceOrderAsync(order);

        result.Success.Should().BeTrue();
        result.ExchangeOrderId.Should().StartWith("PAPER-");
    }

    [Fact]
    public async Task PlaceOrder_Limit_Succeeds()
    {
        var order = new OrderBuilder()
            .WithType(Common.Enums.OrderType.Limit)
            .WithPrice(50_000m)
            .Build();

        var result = await _executor.PlaceOrderAsync(order);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PlaceOrder_StopLoss_Succeeds()
    {
        var order = new OrderBuilder()
            .WithType(Common.Enums.OrderType.StopLoss)
            .WithStopPrice(48_000m)
            .Build();

        var result = await _executor.PlaceOrderAsync(order);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CancelOrder_ExistingOrder_Succeeds()
    {
        var placed = await _executor.PlaceOrderAsync(new OrderBuilder().Build());
        var cancelled = await _executor.CancelOrderAsync(placed.ExchangeOrderId!, "BTCUSDT");

        cancelled.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrderStatus_ExistingOrder_Succeeds()
    {
        var placed = await _executor.PlaceOrderAsync(new OrderBuilder().Build());
        var status = await _executor.GetOrderStatusAsync(placed.ExchangeOrderId!, "BTCUSDT");

        status.Success.Should().BeTrue();
    }
}
