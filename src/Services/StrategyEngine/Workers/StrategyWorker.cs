using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.StrategyEngine.Services;

namespace QuantTrader.StrategyEngine.Workers;

/// <summary>
/// Background service that subscribes to market tick and candle events from the event bus,
/// evaluates all enabled strategies, and publishes trade signals.
///
/// Degradation: when the event bus circuit is open, signals are buffered in a bounded Channel
/// (capacity 100, DropOldest). The buffer drains automatically when the bus recovers.
/// </summary>
public sealed class StrategyWorker : BackgroundService
{
    private const int SignalBufferCapacity = 100;

    private readonly ILogger<StrategyWorker> _logger;
    private readonly IEventBus _eventBus;
    private readonly IStrategyManager _strategyManager;
    private readonly CandleAggregator _candleAggregator;

    // Buffer for signals when the event bus is temporarily unavailable
    private readonly Channel<(TradeSignalGeneratedEvent Signal, string Topic)> _signalBuffer =
        Channel.CreateBounded<(TradeSignalGeneratedEvent, string)>(new BoundedChannelOptions(SignalBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

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
            await _eventBus.SubscribeAsync<MarketTickReceivedEvent>(
                "market.tick",
                async (evt, ct) => await OnTickReceivedAsync(evt, ct),
                stoppingToken);

            await _eventBus.SubscribeAsync<CandleClosedEvent>(
                "candle.closed",
                async (evt, ct) => await OnCandleClosedAsync(evt, ct),
                stoppingToken);

            _logger.LogInformation("StrategyWorker subscribed to market.tick and candle.closed topics");

            // Drain buffer loop — runs in parallel with event subscriptions
            _ = DrainSignalBufferAsync(stoppingToken);

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
            var tick = evt.Tick;
            await _candleAggregator.ProcessTickAsync(tick, TimeSpan.FromHours(1), ct);

            var signals = await _strategyManager.EvaluateAsync(tick, ct);

            foreach (var signal in signals)
            {
                var signalEvent = new TradeSignalGeneratedEvent(
                    Signal: signal,
                    CorrelationId: evt.CorrelationId,
                    Timestamp: DateTimeOffset.UtcNow,
                    Source: "StrategyEngine");

                await PublishOrBufferAsync(signalEvent, "strategy.signal", ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
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
            _logger.LogDebug("Candle appended: {Symbol} [{Interval}] Close={Close}",
                evt.Candle.Symbol, evt.Candle.Interval, evt.Candle.Close);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing closed candle for {Symbol}", evt.Candle.Symbol);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Publishes a signal directly to the event bus.
    /// If the publish throws (bus unavailable), buffers the signal for later retry.
    /// </summary>
    private async Task PublishOrBufferAsync(TradeSignalGeneratedEvent evt, string topic, CancellationToken ct)
    {
        try
        {
            await _eventBus.PublishAsync(evt, topic, ct);
            _logger.LogInformation(
                "Published TradeSignal: {Action} {Symbol} @ {Price} from {Strategy} (confidence={Confidence:F2})",
                evt.Signal.Action, evt.Signal.Symbol, evt.Signal.Price, evt.Signal.Strategy, evt.Signal.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "EventBus unavailable. Buffering signal for {Symbol}. Buffer items: ~{Count}",
                evt.Signal.Symbol, _signalBuffer.Reader.Count);

            await _signalBuffer.Writer.WriteAsync((evt, topic), ct);
        }
    }

    /// <summary>
    /// Continuously attempts to drain the signal buffer when the event bus recovers.
    /// </summary>
    private async Task DrainSignalBufferAsync(CancellationToken ct)
    {
        await foreach (var (signalEvent, topic) in _signalBuffer.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _eventBus.PublishAsync(signalEvent, topic, ct);
                _logger.LogInformation("Drained buffered signal for {Symbol}", signalEvent.Signal.Symbol);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to drain buffered signal. Re-buffering.");
                // Re-queue; if buffer is full, DropOldest policy kicks in
                await _signalBuffer.Writer.WriteAsync((signalEvent, topic), ct);
                await Task.Delay(500, ct); // Back off before retrying
            }
        }
    }
}
