using Microsoft.Extensions.Options;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.DataIngestion.Providers;
using QuantTrader.DataIngestion.Services;

namespace QuantTrader.DataIngestion.Workers;

/// <summary>
/// Background worker that orchestrates real-time market data streaming.
/// Delegates the actual WebSocket/REST work to <see cref="IMarketDataProvider"/>.
/// Supports degradation cascade: WebSocket → REST polling → stale data.
/// </summary>
public sealed class BinanceWebSocketWorker : BackgroundService
{
    private const int MaxWebSocketFailures = 5;
    private const int MaxReconnectDelaySeconds = 120;
    private const int InitialReconnectDelaySeconds = 1;

    private readonly ILogger<BinanceWebSocketWorker> _logger;
    private readonly IMarketDataProvider _primaryProvider;      // WebSocket
    private readonly IMarketDataProvider _fallbackProvider;     // REST polling
    private readonly IEventBus _eventBus;
    private readonly IRedisCacheService _redisCache;
    private readonly List<string> _symbols;

    private int _consecutiveFailures;

    public BinanceWebSocketWorker(
        ILogger<BinanceWebSocketWorker> logger,
        IMarketDataProvider primaryProvider,
        IMarketDataProvider fallbackProvider,
        IEventBus eventBus,
        IRedisCacheService redisCache,
        IOptions<SymbolsOptions> symbolsOptions)
    {
        _logger = logger;
        _primaryProvider = primaryProvider;
        _fallbackProvider = fallbackProvider;
        _eventBus = eventBus;
        _redisCache = redisCache;
        _symbols = symbolsOptions.Value.Symbols;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_symbols.Count == 0)
        {
            _logger.LogWarning("No symbols configured. BinanceWebSocketWorker will not start.");
            return;
        }

        var reconnectDelay = InitialReconnectDelaySeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Choose provider based on failure count
            var activeProvider = _consecutiveFailures >= MaxWebSocketFailures
                ? _fallbackProvider
                : _primaryProvider;

            if (activeProvider == _fallbackProvider && _consecutiveFailures == MaxWebSocketFailures)
            {
                _logger.LogWarning(
                    "WebSocket failed {Count} times. Switching to REST polling fallback.",
                    _consecutiveFailures);

                await PublishDegradedHealthAsync("WebSocket", stoppingToken);
            }

            try
            {
                await activeProvider.StreamAsync(_symbols, OnTickAsync, stoppingToken);
                // Clean exit (cancellation requested) — reset
                _consecutiveFailures = 0;
                reconnectDelay = InitialReconnectDelaySeconds;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("BinanceWebSocketWorker shutting down gracefully.");
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex,
                    "Market data provider '{Provider}' failed (failure #{Count}). Reconnecting in {Delay}s.",
                    activeProvider.Name, _consecutiveFailures, reconnectDelay);

                if (_consecutiveFailures > MaxWebSocketFailures * 2)
                {
                    // Both WebSocket and REST are failing — enter stale data mode
                    await PublishDegradedHealthAsync("MarketData", stoppingToken);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(reconnectDelay), stoppingToken);
                    reconnectDelay = Math.Min(reconnectDelay * 2, MaxReconnectDelaySeconds);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task OnTickAsync(MarketTick tick, CancellationToken ct)
    {
        _consecutiveFailures = 0; // Reset on successful tick

        var @event = new MarketTickReceivedEvent(
            Tick: tick,
            CorrelationId: Guid.NewGuid().ToString("N"),
            Timestamp: DateTimeOffset.UtcNow,
            Source: "BinanceWebSocket");

        _eventBus.Publish(@event);

        _ = _redisCache.SetLatestPriceAsync(tick.Symbol, tick.Price);
    }

    private async Task PublishDegradedHealthAsync(string component, CancellationToken ct)
    {
        try
        {
            var healthEvent = new SystemHealthEvent(
                Service: "DataIngestion",
                Component: component,
                Status: Common.Events.HealthStatus.Degraded,
                Message: $"DataIngestion {component} is degraded. Fallback mode active.",
                CorrelationId: Guid.NewGuid().ToString(),
                Timestamp: DateTimeOffset.UtcNow,
                Source: nameof(BinanceWebSocketWorker));

            _eventBus.Publish(healthEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish degraded health event");
        }
    }
}
