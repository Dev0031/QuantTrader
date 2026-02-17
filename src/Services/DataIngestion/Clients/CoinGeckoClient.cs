using System.Text.Json;

namespace QuantTrader.DataIngestion.Clients;

/// <summary>Client for the CoinGecko free API (no key required).</summary>
public interface ICoinGeckoClient
{
    /// <summary>Get current USD prices for a batch of coin ids.</summary>
    Task<Dictionary<string, decimal>> GetCurrentPricesAsync(string[] ids, CancellationToken cancellationToken = default);

    /// <summary>Get detailed market data for a specific coin (market cap, volume, 24h change).</summary>
    Task<CoinGeckoMarketData?> GetMarketDataAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>Market data snapshot from CoinGecko.</summary>
public sealed record CoinGeckoMarketData(
    string Id,
    decimal MarketCap,
    decimal TotalVolume,
    decimal PriceChangePercent24h,
    decimal CurrentPrice);

public sealed class CoinGeckoClient : ICoinGeckoClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoinGeckoClient> _logger;

    // CoinGecko free tier: ~10-30 req/min
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTimeOffset _lastRequestTime = DateTimeOffset.MinValue;
    private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(2.5);

    public CoinGeckoClient(HttpClient httpClient, ILogger<CoinGeckoClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<string, decimal>> GetCurrentPricesAsync(string[] ids, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (ids.Length == 0)
            return result;

        await ThrottleAsync(cancellationToken);

        var idsParam = string.Join(",", ids);
        var url = $"/simple/price?ids={idsParam}&vs_currencies=usd";

        _logger.LogDebug("CoinGecko price query: {Ids}", idsParam);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.TryGetProperty("usd", out var usdElement))
            {
                result[property.Name] = usdElement.GetDecimal();
            }
        }

        return result;
    }

    public async Task<CoinGeckoMarketData?> GetMarketDataAsync(string id, CancellationToken cancellationToken = default)
    {
        await ThrottleAsync(cancellationToken);

        var url = $"/coins/markets?vs_currency=usd&ids={id}&order=market_cap_desc&per_page=1&page=1&sparkline=false";

        _logger.LogDebug("CoinGecko market data query: {Id}", id);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var items = doc.RootElement;
        if (items.GetArrayLength() == 0)
            return null;

        var item = items[0];
        return new CoinGeckoMarketData(
            Id: id,
            MarketCap: item.GetProperty("market_cap").GetDecimal(),
            TotalVolume: item.GetProperty("total_volume").GetDecimal(),
            PriceChangePercent24h: item.GetProperty("price_change_percentage_24h").GetDecimal(),
            CurrentPrice: item.GetProperty("current_price").GetDecimal());
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var elapsed = DateTimeOffset.UtcNow - _lastRequestTime;
            if (elapsed < MinRequestInterval)
            {
                await Task.Delay(MinRequestInterval - elapsed, cancellationToken);
            }

            _lastRequestTime = DateTimeOffset.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}
