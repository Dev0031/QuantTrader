using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.DataIngestion.Services;
using Websocket.Client;

namespace QuantTrader.DataIngestion.Workers;

/// <summary>
/// Background worker that maintains a persistent WebSocket connection to the Binance exchange,
/// subscribing to real-time trade streams for configured symbols. Reconnects automatically
/// with exponential backoff on disconnection.
/// </summary>
public sealed class BinanceWebSocketWorker : BackgroundService
{
    private readonly ILogger<BinanceWebSocketWorker> _logger;
    private readonly IEventBus _eventBus;
    private readonly IRedisCacheService _redisCache;
    private readonly IDataNormalizerService _normalizer;
    private readonly BinanceSettings _binanceSettings;
    private readonly List<string> _symbols;

    private const int MaxReconnectDelaySeconds = 120;
    private const int InitialReconnectDelaySeconds = 1;

    public BinanceWebSocketWorker(
        ILogger<BinanceWebSocketWorker> logger,
        IEventBus eventBus,
        IRedisCacheService redisCache,
        IDataNormalizerService normalizer,
        IOptions<BinanceSettings> binanceSettings,
        IOptions<SymbolsOptions> symbolsOptions)
    {
        _logger = logger;
        _eventBus = eventBus;
        _redisCache = redisCache;
        _normalizer = normalizer;
        _binanceSettings = binanceSettings.Value;
        _symbols = symbolsOptions.Value.Symbols;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_symbols.Count == 0)
        {
            _logger.LogWarning("No symbols configured for WebSocket streaming. Worker will not start");
            return;
        }

        var reconnectDelay = InitialReconnectDelaySeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunWebSocketAsync(stoppingToken);
                // If we exit cleanly, reset delay
                reconnectDelay = InitialReconnectDelaySeconds;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("BinanceWebSocketWorker shutting down gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket connection failed. Reconnecting in {Delay}s", reconnectDelay);
                await Task.Delay(TimeSpan.FromSeconds(reconnectDelay), stoppingToken);
                reconnectDelay = Math.Min(reconnectDelay * 2, MaxReconnectDelaySeconds);
            }
        }
    }

    private async Task RunWebSocketAsync(CancellationToken stoppingToken)
    {
        // Build combined stream URL: wss://testnet.binance.vision/ws/btcusdt@trade/ethusdt@trade/...
        var streams = string.Join("/", _symbols.Select(s => $"{s.ToLowerInvariant()}@trade"));
        var wsUrl = new Uri($"{_binanceSettings.WebSocketUrl}/{streams}");

        _logger.LogInformation("Connecting to Binance WebSocket at {Url}", wsUrl);

        using var client = new WebsocketClient(wsUrl)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(30)
        };

        client.ReconnectionHappened.Subscribe(info =>
            _logger.LogInformation("WebSocket reconnection occurred: {Type}", info.Type));

        client.DisconnectionHappened.Subscribe(info =>
            _logger.LogWarning("WebSocket disconnected: {Type} {CloseStatus}", info.Type, info.CloseStatus));

        client.MessageReceived.Subscribe(msg => ProcessMessage(msg.Text));

        await client.Start();

        _logger.LogInformation("Binance WebSocket connected. Streaming {Count} symbols: {Symbols}",
            _symbols.Count, string.Join(", ", _symbols));

        // Keep alive until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        await client.Stop(WebSocketCloseStatus.NormalClosure, "Service shutting down");
        _logger.LogInformation("Binance WebSocket disconnected cleanly");
    }

    private void ProcessMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            var tick = _normalizer.NormalizeBinanceTrade(message);
            if (tick is null)
                return;

            // Publish event
            var @event = new MarketTickReceivedEvent(
                Tick: tick,
                CorrelationId: Guid.NewGuid().ToString("N"),
                Timestamp: DateTimeOffset.UtcNow,
                Source: "BinanceWebSocket");

            _eventBus.Publish(@event);

            // Cache latest price in Redis (fire-and-forget with logging)
            _ = _redisCache.SetLatestPriceAsync(tick.Symbol, tick.Price);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse WebSocket message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket message");
        }
    }
}
