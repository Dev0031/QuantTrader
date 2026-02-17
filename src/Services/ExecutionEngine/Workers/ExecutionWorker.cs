using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Services;
using QuantTrader.Infrastructure.Messaging;

namespace QuantTrader.ExecutionEngine.Workers;

/// <summary>
/// Background service that subscribes to OrderApprovedEvent from the event bus,
/// sends orders to Binance via IOrderExecutor, publishes OrderExecutedEvent on success
/// and RiskAlertEvent on failure. Monitors pending orders for fill status.
/// </summary>
public sealed class ExecutionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ExecutionWorker> _logger;
    private readonly ExecutionSettings _settings;

    public ExecutionWorker(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IOptions<ExecutionSettings> settings,
        ILogger<ExecutionWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExecutionWorker starting. Subscribing to {Topic}", EventTopics.ApprovedOrders);

        await _eventBus.SubscribeAsync<OrderApprovedEvent>(
            EventTopics.ApprovedOrders,
            async (evt, ct) => await HandleOrderApprovedAsync(evt, ct).ConfigureAwait(false),
            stoppingToken).ConfigureAwait(false);

        // Monitor pending orders periodically.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorPendingOrdersAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring pending orders");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("ExecutionWorker stopping");
    }

    private async Task HandleOrderApprovedAsync(OrderApprovedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation(
            "Received OrderApprovedEvent: {OrderId} {Symbol} {Side} {Quantity} (CorrelationId: {CorrelationId})",
            evt.Order.Id, evt.Order.Symbol, evt.Order.Side, evt.Order.Quantity, evt.CorrelationId);

        using var scope = _serviceProvider.CreateScope();
        var orderExecutor = scope.ServiceProvider.GetRequiredService<IOrderExecutor>();
        var orderTracker = scope.ServiceProvider.GetRequiredService<IOrderTracker>();
        var positionTracker = scope.ServiceProvider.GetRequiredService<IPositionTracker>();

        var result = await orderExecutor.PlaceOrderAsync(evt.Order, ct).ConfigureAwait(false);

        if (result.Success && result.ExecutedOrder is not null)
        {
            _logger.LogInformation(
                "Order {OrderId} executed successfully. ExchangeOrderId: {ExchangeOrderId}",
                evt.Order.Id, result.ExchangeOrderId);

            // Track the executed order.
            await orderTracker.TrackAsync(result.ExecutedOrder, ct).ConfigureAwait(false);

            // Open or update position if order is filled.
            if (result.ExecutedOrder.Status == OrderStatus.Filled)
            {
                await positionTracker.OpenPositionAsync(result.ExecutedOrder, ct).ConfigureAwait(false);
            }

            // Publish success event.
            var executedEvent = new OrderExecutedEvent(
                Order: result.ExecutedOrder,
                CorrelationId: evt.CorrelationId,
                Timestamp: DateTimeOffset.UtcNow,
                Source: "ExecutionEngine");

            await _eventBus.PublishAsync(executedEvent, EventTopics.ExecutedOrders, ct).ConfigureAwait(false);
        }
        else
        {
            _logger.LogError(
                "Order {OrderId} execution failed: {Error}",
                evt.Order.Id, result.ErrorMessage);

            // Publish failure as risk alert.
            var riskAlert = new RiskAlertEvent(
                AlertType: "OrderExecutionFailure",
                Message: $"Order {evt.Order.Id} for {evt.Order.Symbol} failed: {result.ErrorMessage}",
                Symbol: evt.Order.Symbol,
                Severity: 0.8,
                CorrelationId: evt.CorrelationId,
                Timestamp: DateTimeOffset.UtcNow,
                Source: "ExecutionEngine");

            await _eventBus.PublishAsync(riskAlert, EventTopics.RiskAlerts, ct).ConfigureAwait(false);
        }
    }

    private async Task MonitorPendingOrdersAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var orderTracker = scope.ServiceProvider.GetRequiredService<IOrderTracker>();
        var orderExecutor = scope.ServiceProvider.GetRequiredService<IOrderExecutor>();
        var positionTracker = scope.ServiceProvider.GetRequiredService<IPositionTracker>();

        var pendingOrders = await orderTracker.GetPendingOrdersAsync(ct).ConfigureAwait(false);

        if (pendingOrders.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Monitoring {Count} pending orders", pendingOrders.Count);

        foreach (var order in pendingOrders)
        {
            if (ct.IsCancellationRequested) break;

            if (string.IsNullOrEmpty(order.ExchangeOrderId))
            {
                continue;
            }

            // Check for timed-out orders.
            var elapsed = DateTimeOffset.UtcNow - order.Timestamp;
            if (elapsed.TotalSeconds > _settings.OrderTimeoutSeconds && order.Status == OrderStatus.New)
            {
                _logger.LogWarning("Order {ExchangeOrderId} timed out after {Seconds}s. Cancelling.", order.ExchangeOrderId, elapsed.TotalSeconds);
                await orderExecutor.CancelOrderAsync(order.ExchangeOrderId, order.Symbol, ct).ConfigureAwait(false);
                await orderTracker.UpdateStatusAsync(order.ExchangeOrderId, OrderStatus.Canceled, order.FilledQuantity, order.FilledPrice, ct).ConfigureAwait(false);
                continue;
            }

            // Query current status from exchange.
            var statusResult = await orderExecutor.GetOrderStatusAsync(order.ExchangeOrderId, order.Symbol, ct).ConfigureAwait(false);

            if (statusResult.Success && statusResult.ExecutedOrder is not null)
            {
                var updatedOrder = statusResult.ExecutedOrder;

                if (updatedOrder.Status != order.Status)
                {
                    _logger.LogInformation(
                        "Order {ExchangeOrderId} status changed: {OldStatus} -> {NewStatus}",
                        order.ExchangeOrderId, order.Status, updatedOrder.Status);

                    await orderTracker.UpdateStatusAsync(
                        order.ExchangeOrderId,
                        updatedOrder.Status,
                        updatedOrder.FilledQuantity,
                        updatedOrder.FilledPrice,
                        ct).ConfigureAwait(false);

                    // Open position when order becomes filled.
                    if (updatedOrder.Status == OrderStatus.Filled)
                    {
                        await positionTracker.OpenPositionAsync(updatedOrder, ct).ConfigureAwait(false);

                        var executedEvent = new OrderExecutedEvent(
                            Order: updatedOrder,
                            CorrelationId: Guid.NewGuid().ToString(),
                            Timestamp: DateTimeOffset.UtcNow,
                            Source: "ExecutionEngine");

                        await _eventBus.PublishAsync(executedEvent, EventTopics.ExecutedOrders, ct).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
