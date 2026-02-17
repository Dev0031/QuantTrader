using Microsoft.Extensions.Logging;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.StrategyEngine.Services;

namespace QuantTrader.StrategyEngine.Workers;

/// <summary>
/// Background service that subscribes to market tick and candle events from the event bus,
/// evaluates all enabled strategies, and publishes trade signals for valid results.
/// </summary>
public sealed class StrategyWorker : BackgroundService
{
    private readonly ILogger<StrategyWorker> _logger;
    private readonly IEventBus _eventBus;
    private readonly IStrategyManager _strategyManager;
    private readonly CandleAggregator _candleAggregator;

    public StrategyWorker(
        ILogger<StrategyWorker> logger,
        IEventBus eventBus,
        IStrategyManager strategyManager,
        CandleAggregator candleAggregator)
    {
        _logger = logger;
        _eventBus = eventBus;
        _strategyManager = strategyManager;
        _candleAggregator = candleAggregator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StrategyWorker starting. Subscribing to market events...");

        try
        {
            // Subscribe to market tick events
            await _eventBus.SubscribeAsync<MarketTickReceivedEvent>(
                "market.tick",
                async (evt, ct) => await OnTickReceivedAsync(evt, ct),
                stoppingToken);

            // Subscribe to candle closed events
            await _eventBus.SubscribeAsync<CandleClosedEvent>(
                "candle.closed",
                async (evt, ct) => await OnCandleClosedAsync(evt, ct),
                stoppingToken);

            _logger.LogInformation("StrategyWorker subscribed to market.tick and candle.closed topics");

            // Keep alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("StrategyWorker stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "StrategyWorker encountered a fatal error");
            throw;
        }
    }

    private async Task OnTickReceivedAsync(MarketTickReceivedEvent evt, CancellationToken ct)
    {
        try
        {
            MarketTick tick = evt.Tick;

            // Aggregate tick into candles (1-hour default interval)
            await _candleAggregator.ProcessTickAsync(tick, TimeSpan.FromHours(1), ct);

            // Evaluate all strategies against this tick
            var signals = await _strategyManager.EvaluateAsync(tick, ct);

            foreach (var signal in signals)
            {
                var signalEvent = new TradeSignalGeneratedEvent(
                    Signal: signal,
                    CorrelationId: evt.CorrelationId,
                    Timestamp: DateTimeOffset.UtcNow,
                    Source: "StrategyEngine");

                await _eventBus.PublishAsync(signalEvent, "strategy.signal", ct);

                _logger.LogInformation(
                    "Published TradeSignal: {Action} {Symbol} @ {Price} from {Strategy} (confidence={Confidence:F2})",
                    signal.Action, signal.Symbol, signal.Price, signal.Strategy, signal.ConfidenceScore);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing tick for {Symbol}", evt.Tick.Symbol);
        }
    }

    private Task OnCandleClosedAsync(CandleClosedEvent evt, CancellationToken ct)
    {
        try
        {
            _strategyManager.AppendCandle(evt.Candle);

            _logger.LogDebug(
                "Candle appended: {Symbol} [{Interval}] Close={Close} Volume={Volume}",
                evt.Candle.Symbol, evt.Candle.Interval, evt.Candle.Close, evt.Candle.Volume);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing closed candle for {Symbol}", evt.Candle.Symbol);
        }

        return Task.CompletedTask;
    }
}
