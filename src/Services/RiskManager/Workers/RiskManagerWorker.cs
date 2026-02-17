using Microsoft.Extensions.Logging;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.RiskManager.Services;

namespace QuantTrader.RiskManager.Workers;

/// <summary>
/// Background service that subscribes to trade signals, evaluates risk,
/// and publishes approved orders or risk alerts. Continuously monitors
/// the portfolio for kill-switch conditions.
/// </summary>
public sealed class RiskManagerWorker : BackgroundService
{
    private static readonly TimeSpan PortfolioMonitorInterval = TimeSpan.FromSeconds(5);

    private readonly IEventBus _eventBus;
    private readonly IRiskEvaluator _riskEvaluator;
    private readonly IKillSwitchManager _killSwitchManager;
    private readonly IDrawdownMonitor _drawdownMonitor;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<RiskManagerWorker> _logger;

    public RiskManagerWorker(
        IEventBus eventBus,
        IRiskEvaluator riskEvaluator,
        IKillSwitchManager killSwitchManager,
        IDrawdownMonitor drawdownMonitor,
        IRedisCacheService cache,
        ILogger<RiskManagerWorker> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _riskEvaluator = riskEvaluator ?? throw new ArgumentNullException(nameof(riskEvaluator));
        _killSwitchManager = killSwitchManager ?? throw new ArgumentNullException(nameof(killSwitchManager));
        _drawdownMonitor = drawdownMonitor ?? throw new ArgumentNullException(nameof(drawdownMonitor));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RiskManager worker starting");

        // Subscribe to trade signals
        await _eventBus.SubscribeAsync<TradeSignalGeneratedEvent>(
            EventTopics.TradeSignals,
            HandleTradeSignalAsync,
            stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("Subscribed to {Topic} topic", EventTopics.TradeSignals);

        // Continuous portfolio monitoring loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorPortfolioAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during portfolio monitoring cycle");
            }

            await Task.Delay(PortfolioMonitorInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("RiskManager worker stopping");
    }

    private async Task HandleTradeSignalAsync(TradeSignalGeneratedEvent signalEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "Received trade signal {SignalId} for {Symbol} from {Source}",
            signalEvent.Signal.Id, signalEvent.Signal.Symbol, signalEvent.Source);

        try
        {
            var result = await _riskEvaluator.EvaluateSignalAsync(signalEvent.Signal, ct).ConfigureAwait(false);

            if (result.Approved && result.ApprovedOrder is not null)
            {
                var approvedEvent = new OrderApprovedEvent(
                    Order: result.ApprovedOrder,
                    CorrelationId: signalEvent.CorrelationId,
                    Timestamp: DateTimeOffset.UtcNow,
                    Source: nameof(RiskManagerWorker));

                await _eventBus.PublishAsync(approvedEvent, EventTopics.ApprovedOrders, ct)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Order approved and published for signal {SignalId}: {Side} {Quantity} {Symbol}",
                    signalEvent.Signal.Id,
                    result.ApprovedOrder.Side,
                    result.ApprovedOrder.Quantity,
                    result.ApprovedOrder.Symbol);
            }
            else
            {
                var alertEvent = new RiskAlertEvent(
                    AlertType: "SignalRejected",
                    Message: result.RejectionReason ?? "Unknown rejection reason",
                    Symbol: signalEvent.Signal.Symbol,
                    Severity: 0.7,
                    CorrelationId: signalEvent.CorrelationId,
                    Timestamp: DateTimeOffset.UtcNow,
                    Source: nameof(RiskManagerWorker));

                await _eventBus.PublishAsync(alertEvent, EventTopics.RiskAlerts, ct)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "Signal {SignalId} rejected: {Reason}",
                    signalEvent.Signal.Id, result.RejectionReason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating signal {SignalId}", signalEvent.Signal.Id);
        }
    }

    private async Task MonitorPortfolioAsync(CancellationToken ct)
    {
        var portfolio = await _cache.GetPortfolioSnapshotAsync(ct).ConfigureAwait(false);
        if (portfolio is null) return;

        // Update drawdown monitor with latest equity
        await _drawdownMonitor.UpdateEquityAsync(portfolio.TotalEquity, ct).ConfigureAwait(false);

        // Check kill-switch conditions
        await _killSwitchManager.CheckConditionsAsync(portfolio, ct).ConfigureAwait(false);
    }
}
