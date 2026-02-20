using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Models;
using QuantTrader.DataIngestion.Services;

namespace QuantTrader.DataIngestion.Providers;

/// <summary>
/// Streams real-time trade data from Binance via WebSocket combined streams.
/// Extracted from BinanceWebSocketWorker so the worker becomes a thin orchestrator.
/// </summary>
public sealed class BinanceWebSocketProvider : IMarketDataProvider
{
    private readonly IWebSocketClientFactory _wsFactory;
    private readonly IDataNormalizerService _normalizer;
    private readonly BinanceSettings _settings;
    private readonly ILogger<BinanceWebSocketProvider> _logger;

    public string Name => "BinanceWebSocket";

    public BinanceWebSocketProvider(
        IWebSocketClientFactory wsFactory,
        IDataNormalizerService normalizer,
        IOptions<BinanceSettings> settings,
        ILogger<BinanceWebSocketProvider> logger)
    {
        _wsFactory = wsFactory ?? throw new ArgumentNullException(nameof(wsFactory));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StreamAsync(
        IReadOnlyList<string> symbols,
        Func<MarketTick, CancellationToken, Task> onTick,
        CancellationToken ct)
    {
        var streams = string.Join("/", symbols.Select(s => $"{s.ToLowerInvariant()}@trade"));
        var wsUrl = new Uri($"{_settings.WebSocketUrl}/{streams}");

        _logger.LogInformation("{Provider}: Connecting to {Url}", Name, wsUrl);

        using var client = _wsFactory.Create(wsUrl);

        client.ReconnectionHappened.Subscribe(info =>
            _logger.LogInformation("{Provider}: WebSocket reconnection: {Type}", Name, info.Type));

        client.DisconnectionHappened.Subscribe(info =>
            _logger.LogWarning("{Provider}: WebSocket disconnected: {Type} {Status}", Name, info.Type, info.CloseStatus));

        client.MessageReceived.Subscribe(msg => OnMessageReceived(msg.Text, onTick, ct));

        await client.Start();

        _logger.LogInformation("{Provider}: Connected. Streaming {Count} symbols: {Symbols}",
            Name, symbols.Count, string.Join(", ", symbols));

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        await client.Stop(WebSocketCloseStatus.NormalClosure, "Service shutting down");
        _logger.LogInformation("{Provider}: Disconnected cleanly", Name);
    }

    private void OnMessageReceived(
        string? message,
        Func<MarketTick, CancellationToken, Task> onTick,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            var tick = _normalizer.NormalizeBinanceTrade(message);
            if (tick is null) return;

            _ = onTick(tick, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "{Provider}: Failed to parse WebSocket message", Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider}: Error processing WebSocket message", Name);
        }
    }
}
