using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Clients;
using QuantTrader.ExecutionEngine.Models;
using QuantTrader.ExecutionEngine.Services;
using QuantTrader.TestInfrastructure.Helpers;

namespace ExecutionEngine.Tests;

public class OrderExecutorTests
{
    private readonly Mock<IBinanceTradeClient> _tradeClientMock;
    private readonly OrderExecutor _executor;

    public OrderExecutorTests()
    {
        _tradeClientMock = new Mock<IBinanceTradeClient>();

        // Wire through OrderAdapterFactory in Live mode so IBinanceTradeClient mock is exercised
        var liveAdapter = new LiveOrderAdapter(_tradeClientMock.Object, NullLogger<LiveOrderAdapter>.Instance);
        var paperAdapter = new PaperOrderAdapter(
            new FakeRedisCacheService(),
            Options.Create(new TradingModeSettings { PaperFillLatencyMs = 0 }),
            NullLogger<PaperOrderAdapter>.Instance);

        var modeProvider = new Mock<ITradingModeProvider>();
        modeProvider.Setup(m => m.CurrentMode).Returns(TradingMode.Live);

        var factory = new OrderAdapterFactory(modeProvider.Object, liveAdapter, paperAdapter);

        var settings = Options.Create(new ExecutionSettings
        {
            MaxRetries = 3,
            RetryDelayMs = 0  // no delay in tests
        });

        _executor = new OrderExecutor(factory, settings, NullLogger<OrderExecutor>.Instance);
    }

    private static Order CreateTestOrder(OrderType type = OrderType.Market)
    {
        return new Order(
            Id: Guid.NewGuid(),
            ExchangeOrderId: null,
            Symbol: "BTCUSDT",
            Side: OrderSide.Buy,
            Type: type,
            Quantity: 0.1m,
            Price: 50_000m,
            StopPrice: null,
            Status: OrderStatus.New,
            FilledQuantity: 0m,
            FilledPrice: 0m,
            Commission: 0m,
            Timestamp: DateTimeOffset.UtcNow,
            UpdatedAt: null);
    }

    [Fact]
    public async Task Test_PlaceOrder_Success()
    {
        // Arrange
        var order = CreateTestOrder();
        var expectedResult = new OrderResult(
            Success: true,
            ExecutedOrder: order,
            ErrorMessage: null,
            ExchangeOrderId: "EX123");

        _tradeClientMock
            .Setup(c => c.PlaceMarketOrderAsync(
                It.IsAny<string>(), It.IsAny<OrderSide>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _executor.PlaceOrderAsync(order);

        // Assert
        result.Success.Should().BeTrue();
        result.ExchangeOrderId.Should().Be("EX123");
        _tradeClientMock.Verify(
            c => c.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.1m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Test_PlaceOrder_Retries_OnTransientError()
    {
        // Arrange
        var order = CreateTestOrder();
        var failResult = new OrderResult(false, null, "Transient error", null);
        var successResult = new OrderResult(true, order, null, "EX456");

        _tradeClientMock
            .SetupSequence(c => c.PlaceMarketOrderAsync(
                It.IsAny<string>(), It.IsAny<OrderSide>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failResult)
            .ReturnsAsync(failResult)
            .ReturnsAsync(successResult);

        // Act
        var result = await _executor.PlaceOrderAsync(order);

        // Assert
        result.Success.Should().BeTrue();
        result.ExchangeOrderId.Should().Be("EX456");
        _tradeClientMock.Verify(
            c => c.PlaceMarketOrderAsync(It.IsAny<string>(), It.IsAny<OrderSide>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Test_PlaceOrder_FailsAfterMaxRetries()
    {
        // Arrange
        var order = CreateTestOrder();
        var failResult = new OrderResult(false, null, "Persistent error", null);

        _tradeClientMock
            .Setup(c => c.PlaceMarketOrderAsync(
                It.IsAny<string>(), It.IsAny<OrderSide>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failResult);

        // Act
        var result = await _executor.PlaceOrderAsync(order);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Persistent error");
        _tradeClientMock.Verify(
            c => c.PlaceMarketOrderAsync(It.IsAny<string>(), It.IsAny<OrderSide>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
}
