using Microsoft.AspNetCore.SignalR;
using QuantTrader.ApiGateway.Hubs;
using QuantTrader.Common.Events;
using QuantTrader.Infrastructure.Messaging;

namespace QuantTrader.ApiGateway.Services;

/// <summary>
/// Background service that subscribes to event bus topics and forwards updates
/// to connected SignalR clients in real time.
/// Also logs each event to the activity feed for system transparency.
/// </summary>
public sealed class RealTimeNotifier : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IHubContext<TradingHub> _hubContext;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<RealTimeNotifier> _logger;

    private long _tickCount;

    public RealTimeNotifier(
        IEventBus eventBus,
        IHubContext<TradingHub> hubContext,
        IActivityLogService activityLog,
        ILogger<RealTimeNotifier> logger)
    {
        _eventBus = eventBus;
        _hubContext = hubContext;
        _activityLog = activityLog;
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
                _tickCount++;

                var payload = new
                {
                    tick.Symbol,
                    tick.Price,
                    tick.Volume,
                    tick.BidPrice,
                    tick.AskPrice,
                    tick.Timestamp,
                    Source = tickEvent.Source
                };

                // Push to the symbol-specific group
                await _hubContext.Clients
                    .Group($"symbol:{tick.Symbol}")
                    .SendAsync("OnTickUpdate", payload, token);

                // Push to the general prices group
                await _hubContext.Clients
                    .Group("prices")
                    .SendAsync("OnTickUpdate", payload, token);

                // Log to activity feed every 50 ticks to avoid flooding
                if (_tickCount % 50 == 1)
                {
                    await _activityLog.LogAsync(
                        tickEvent.Source ?? "DataIngestion",
                        "info",
                        $"{tick.Symbol} @ ${tick.Price:N2}  |  {_tickCount:N0} ticks pushed to dashboard",
                        tick.Symbol, token);
                }
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

                var tradePaylod = new
                {
                    order.Id,
                    order.Symbol,
                    Side = order.Side.ToString(),
                    order.Quantity,
                    order.FilledPrice,
                    Status = order.Status.ToString(),
                    order.Timestamp
                };

                await _hubContext.Clients
                    .Group("trades")
                    .SendAsync("OnTradeExecuted", tradePaylod, token);

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

                await _activityLog.LogAsync(
                    "ExecutionEngine", "success",
                    $"Trade executed: {order.Side} {order.Quantity} {order.Symbol} @ ${order.FilledPrice:N2}",
                    order.Symbol, token);
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

                var level = alertEvent.Severity >= 0.8 ? "error"
                          : alertEvent.Severity >= 0.5 ? "warning"
                          : "info";

                await _activityLog.LogAsync(
                    "RiskManager", level,
                    $"Risk alert: {alertEvent.AlertType} — {alertEvent.Message}",
                    alertEvent.Symbol, token);
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
