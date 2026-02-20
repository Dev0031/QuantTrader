using Microsoft.Extensions.Logging;
using QuantTrader.Common.Models;
using QuantTrader.DataIngestion.Clients;

namespace QuantTrader.DataIngestion.Providers;

/// <summary>
/// Fallback market data provider that polls the Binance REST API for prices.
/// Activated automatically when the WebSocket connection fails repeatedly.
/// Provides stale but functional data during WebSocket outages.
/// </summary>
public sealed class RestPollingMarketDataProvider : IMarketDataProvider
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IBinanceRestClient _restClient;
    private readonly ILogger<RestPollingMarketDataProvider> _logger;

    public string Name => "BinanceRestPolling";

    public RestPollingMarketDataProvider(
        IBinanceRestClient restClient,
        ILogger<RestPollingMarketDataProvider> logger)
    {
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StreamAsync(
        IReadOnlyList<string> symbols,
        Func<MarketTick, CancellationToken, Task> onTick,
        CancellationToken ct)
    {
        _logger.LogWarning("{Provider}: REST polling fallback activated for {Count} symbols", Name, symbols.Count);

        while (!ct.IsCancellationRequested)
        {
            foreach (var symbol in symbols)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var price = await _restClient.GetTickerPriceAsync(symbol, ct);
                    if (price > 0)
                    {
                        var tick = new MarketTick(
                            Symbol: symbol,
                            Price: price,
                            Volume: 0m,
                            BidPrice: price,
                            AskPrice: price,
                            Timestamp: DateTimeOffset.UtcNow);

                        await onTick(tick, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Provider}: Failed to poll {Symbol}", Name, symbol);
                }
            }

            await Task.Delay(PollInterval, ct);
        }
    }
}
