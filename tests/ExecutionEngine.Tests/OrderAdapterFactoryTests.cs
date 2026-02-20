using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Clients;
using QuantTrader.TestInfrastructure.Helpers;

namespace QuantTrader.ExecutionEngine.Tests;

public sealed class OrderAdapterFactoryTests
{
    private readonly Mock<ITradingModeProvider> _modeMock = new();
    private readonly LiveOrderAdapter _live;
    private readonly PaperOrderAdapter _paper;
    private readonly OrderAdapterFactory _factory;

    public OrderAdapterFactoryTests()
    {
        var tradeClient = new Mock<IBinanceTradeClient>();
        _live = new LiveOrderAdapter(tradeClient.Object, NullLogger<LiveOrderAdapter>.Instance);

        var redis = new FakeRedisCacheService();
        var settings = Options.Create(new TradingModeSettings { PaperFillLatencyMs = 0 });
        _paper = new PaperOrderAdapter(redis, settings, NullLogger<PaperOrderAdapter>.Instance);

        _factory = new OrderAdapterFactory(_modeMock.Object, _live, _paper);
    }

    [Fact]
    public void Current_WhenLiveMode_ReturnLiveAdapter()
    {
        _modeMock.Setup(m => m.CurrentMode).Returns(TradingMode.Live);
        _factory.Current.Should().BeSameAs(_live);
    }

    [Fact]
    public void Current_WhenPaperMode_ReturnPaperAdapter()
    {
        _modeMock.Setup(m => m.CurrentMode).Returns(TradingMode.Paper);
        _factory.Current.Should().BeSameAs(_paper);
    }

    [Fact]
    public void Current_WhenSimulationMode_ReturnPaperAdapter()
    {
        _modeMock.Setup(m => m.CurrentMode).Returns(TradingMode.Simulation);
        _factory.Current.Should().BeSameAs(_paper);
    }

    [Fact]
    public void Current_WhenBacktestMode_ReturnPaperAdapter()
    {
        _modeMock.Setup(m => m.CurrentMode).Returns(TradingMode.Backtest);
        _factory.Current.Should().BeSameAs(_paper);
    }

    [Fact]
    public void Current_AfterModeSwitch_ReflectsNewMode()
    {
        _modeMock.Setup(m => m.CurrentMode).Returns(TradingMode.Live);
        _factory.Current.Should().BeSameAs(_live);

        _modeMock.Setup(m => m.CurrentMode).Returns(TradingMode.Paper);
        _factory.Current.Should().BeSameAs(_paper);
    }
}
