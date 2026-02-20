using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Clients;
using QuantTrader.ExecutionEngine.Models;
using QuantTrader.ExecutionEngine.Services;
using QuantTrader.Infrastructure.Redis;

namespace ExecutionEngine.Tests;

public class OrderExecutorTests
{
    private readonly Mock<IBinanceTradeClient> _tradeClientMock;
    private readonly OrderExecutor _executor;

    public OrderExecutorTests()
    {
        _tradeClientMock = new Mock<IBinanceTradeClient>();

        var settings = Options.Create(new ExecutionSettings
        {
            MaxRetries = 3,
            RetryDelayMs = 10
        });

        // Wire LiveOrderAdapter with the mocked trade client
        var liveAdapter = new LiveOrderAdapter(
            _tradeClientMock.Object,
            Mock.Of<ILogger<LiveOrderAdapter>>());

        // PaperOrderAdapter needs a cache and settings — use mocks
        var cacheMock = new Mock<IRedisCacheService>();
        var paperAdapter = new PaperOrderAdapter(
            cacheMock.Object,
            Options.Create(new TradingModeSettings { Mode = TradingMode.Paper }),
            Mock.Of<ILogger<PaperOrderAdapter>>());

        // Set trading mode to Live so factory routes to LiveOrderAdapter
        var modeProviderMock = new Mock<ITradingModeProvider>();
        modeProviderMock.Setup(m => m.CurrentMode).Returns(TradingMode.Live);

        var factory = new OrderAdapterFactory(modeProviderMock.Object, liveAdapter, paperAdapter);

        _executor = new OrderExecutor(factory, settings, Mock.Of<ILogger<OrderExecutor>>());
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
