using Microsoft.AspNetCore.SignalR;
using QuantTrader.ApiGateway.Hubs;
using QuantTrader.Common.Events;
using QuantTrader.Infrastructure.Messaging;

namespace QuantTrader.ApiGateway.Services;

/// <summary>
/// Background service that subscribes to event bus topics and forwards updates
/// to connected SignalR clients in real time.
/// </summary>
public sealed class RealTimeNotifier : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IHubContext<TradingHub> _hubContext;
    private readonly ILogger<RealTimeNotifier> _logger;

    public RealTimeNotifier(
        IEventBus eventBus,
        IHubContext<TradingHub> hubContext,
        ILogger<RealTimeNotifier> logger)
    {
        _eventBus = eventBus;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RealTimeNotifier starting event bus subscriptions");

        var subscriptions = new[]
        {
            SubscribeToTicks(stoppingToken),
            SubscribeToTrades(stoppingToken),
            SubscribeToRiskAlerts(stoppingToken),
            SubscribeToKillSwitch(stoppingToken)
        };

        await Task.WhenAll(subscriptions);
    }

    private async Task SubscribeToTicks(CancellationToken ct)
    {
        await _eventBus.SubscribeAsync<MarketTickReceivedEvent>(
            EventTopics.MarketTicks,
            async (tickEvent, token) =>
            {
                var tick = tickEvent.Tick;

                // Push to the symbol-specific group
                await _hubContext.Clients
                    .Group($"symbol:{tick.Symbol}")
                    .SendAsync("OnTickUpdate", new
                    {
                        tick.Symbol,
                        tick.Price,
                        tick.Volume,
                        tick.BidPrice,
                        tick.AskPrice,
                        tick.Timestamp
                    }, token);

                // Push to the general prices group
                await _hubContext.Clients
                    .Group("prices")
                    .SendAsync("OnTickUpdate", new
                    {
                        tick.Symbol,
                        tick.Price,
                        tick.Timestamp
                    }, token);
            },
            ct);
    }

    private async Task SubscribeToTrades(CancellationToken ct)
    {
        await _eventBus.SubscribeAsync<OrderExecutedEvent>(
            EventTopics.ExecutedOrders,
            async (orderEvent, token) =>
            {
                var order = orderEvent.Order;
                _logger.LogInformation("Broadcasting trade execution: {Symbol} {Side} {Qty}",
                    order.Symbol, order.Side, order.Quantity);

                await _hubContext.Clients
                    .Group("trades")
                    .SendAsync("OnTradeExecuted", new
                    {
                        order.Id,
                        order.Symbol,
                        Side = order.Side.ToString(),
                        order.Quantity,
                        order.FilledPrice,
                        Status = order.Status.ToString(),
                        order.Timestamp
                    }, token);

                // Also push position update to the symbol group
                await _hubContext.Clients
                    .Group($"symbol:{order.Symbol}")
                    .SendAsync("OnPositionUpdate", new
                    {
                        order.Symbol,
                        Side = order.Side.ToString(),
                        order.Quantity,
                        order.FilledPrice,
                        order.Timestamp
                    }, token);
            },
            ct);
    }

    private async Task SubscribeToRiskAlerts(CancellationToken ct)
    {
        await _eventBus.SubscribeAsync<RiskAlertEvent>(
            EventTopics.RiskAlerts,
            async (alertEvent, token) =>
            {
                _logger.LogWarning("Broadcasting risk alert: {AlertType} - {Message}",
                    alertEvent.AlertType, alertEvent.Message);

                await _hubContext.Clients
                    .All
                    .SendAsync("OnRiskAlert", new
                    {
                        alertEvent.AlertType,
                        alertEvent.Message,
                        alertEvent.Symbol,
                        alertEvent.Severity,
                        alertEvent.Timestamp
                    }, token);
            },
            ct);
    }

    private async Task SubscribeToKillSwitch(CancellationToken ct)
    {
        await _eventBus.SubscribeAsync<KillSwitchTriggeredEvent>(
            EventTopics.KillSwitch,
            async (killEvent, token) =>
            {
                _logger.LogCritical("Broadcasting kill switch event: {Reason}", killEvent.Reason);

                await _hubContext.Clients
                    .All
                    .SendAsync("OnKillSwitch", new
                    {
                        killEvent.Reason,
                        killEvent.DrawdownPercent,
                        killEvent.Timestamp
                    }, token);
            },
            ct);
    }
}
