using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.DataIngestion.Clients;
using QuantTrader.DataIngestion.Services;

namespace QuantTrader.DataIngestion.Workers;

/// <summary>
/// Background worker that periodically polls external APIs (CoinGecko for market data,
/// CryptoPanic for news) and persists candle data from Binance to the database.
/// </summary>
public sealed class MarketDataPollingWorker : BackgroundService
{
    private readonly ILogger<MarketDataPollingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICoinGeckoClient _coinGeckoClient;
    private readonly ICryptoPanicClient _cryptoPanicClient;
    private readonly IBinanceRestClient _binanceRestClient;
    private readonly IRedisCacheService _redisCache;
    private readonly IDataNormalizerService _normalizer;
    private readonly List<string> _symbols;
    private readonly CryptoPanicSettings _cryptoPanicSettings;

    private static readonly TimeSpan CoinGeckoInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CryptoPanicInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CandlePersistInterval = TimeSpan.FromMinutes(1);

    // CoinGecko uses lowercase ids, not Binance symbols
    private static readonly Dictionary<string, string> SymbolToCoinGeckoId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTCUSDT"] = "bitcoin",
        ["ETHUSDT"] = "ethereum",
        ["BNBUSDT"] = "binancecoin",
        ["SOLUSDT"] = "solana",
        ["XRPUSDT"] = "ripple"
    };

    public MarketDataPollingWorker(
        ILogger<MarketDataPollingWorker> logger,
        IServiceScopeFactory scopeFactory,
        ICoinGeckoClient coinGeckoClient,
        ICryptoPanicClient cryptoPanicClient,
        IBinanceRestClient binanceRestClient,
        IRedisCacheService redisCache,
        IDataNormalizerService normalizer,
        IOptions<SymbolsOptions> symbolsOptions,
        IOptions<CryptoPanicSettings> cryptoPanicSettings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _coinGeckoClient = coinGeckoClient;
        _cryptoPanicClient = cryptoPanicClient;
        _binanceRestClient = binanceRestClient;
        _redisCache = redisCache;
        _normalizer = normalizer;
        _symbols = symbolsOptions.Value.Symbols;
        _cryptoPanicSettings = cryptoPanicSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataPollingWorker started. Tracking {Count} symbols", _symbols.Count);

        // Stagger startup to avoid thundering-herd on APIs
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        var coinGeckoTimer = Task.CompletedTask;
        var cryptoPanicTimer = Task.CompletedTask;
        var candleTimer = Task.CompletedTask;

        var lastCoinGeckoPoll = DateTimeOffset.MinValue;
        var lastCryptoPanicPoll = DateTimeOffset.MinValue;
        var lastCandlePersist = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            if (now - lastCoinGeckoPoll >= CoinGeckoInterval)
            {
                lastCoinGeckoPoll = now;
                _ = PollCoinGeckoAsync(stoppingToken);
            }

            if (now - lastCryptoPanicPoll >= CryptoPanicInterval)
            {
                lastCryptoPanicPoll = now;
                _ = PollCryptoPanicAsync(stoppingToken);
            }

            if (now - lastCandlePersist >= CandlePersistInterval)
            {
                lastCandlePersist = now;
                _ = PersistCandleDataAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task PollCoinGeckoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var coinGeckoIds = _symbols
                .Where(s => SymbolToCoinGeckoId.ContainsKey(s))
                .Select(s => SymbolToCoinGeckoId[s])
                .ToArray();

            if (coinGeckoIds.Length == 0)
                return;

            var prices = await _coinGeckoClient.GetCurrentPricesAsync(coinGeckoIds, cancellationToken);

            foreach (var (id, price) in prices)
            {
                var symbol = SymbolToCoinGeckoId.FirstOrDefault(x => x.Value == id).Key;
                if (symbol is not null)
                {
                    await _redisCache.SetLatestPriceAsync($"coingecko:{symbol}", price, cancellationToken);
                }
            }

            _logger.LogDebug("CoinGecko poll complete. Received {Count} prices", prices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoinGecko polling failed");
        }
    }

    private async Task PollCryptoPanicAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_cryptoPanicSettings.ApiKey))
        {
            _logger.LogDebug("CryptoPanic API key not configured. Skipping news poll");
            return;
        }

        try
        {
            var news = await _cryptoPanicClient.GetLatestNewsAsync(filter: "hot", cancellationToken: cancellationToken);
            _logger.LogInformation("CryptoPanic poll complete. Received {Count} news items", news.Count);

            // Cache the latest news items
            foreach (var item in news.Take(10))
            {
                await _redisCache.SetAsync(
                    $"news:{item.Id}",
                    System.Text.Json.JsonSerializer.Serialize(item),
                    TimeSpan.FromHours(1),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CryptoPanic polling failed");
        }
    }

    private async Task PersistCandleDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var persistence = scope.ServiceProvider.GetRequiredService<IDataPersistenceService>();

            foreach (var symbol in _symbols)
            {
                try
                {
                    var klines = await _binanceRestClient.GetKlinesAsync(symbol, "1m", limit: 5, cancellationToken: cancellationToken);

                    foreach (var kline in klines)
                    {
                        var candle = _normalizer.NormalizeBinanceKline(symbol, "1m", kline);
                        if (candle is not null)
                        {
                            await persistence.SaveCandleAsync(candle, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist candle data for {Symbol}", symbol);
                }
            }

            _logger.LogDebug("Candle data persistence cycle complete for {Count} symbols", _symbols.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in candle data persistence cycle");
        }
    }
}
