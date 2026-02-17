using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Constants;

namespace QuantTrader.DataIngestion.Clients;

/// <summary>REST client for Binance exchange API with HMAC-SHA256 signing for authenticated endpoints.</summary>
public interface IBinanceRestClient
{
    /// <summary>Fetch historical kline/candlestick data.</summary>
    Task<List<JsonElement>> GetKlinesAsync(string symbol, string interval, int limit = 500, CancellationToken cancellationToken = default);

    /// <summary>Get the current ticker price for a symbol.</summary>
    Task<decimal> GetTickerPriceAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>Get account information including balances. Requires API key and secret.</summary>
    Task<JsonElement> GetAccountInfoAsync(CancellationToken cancellationToken = default);
}

public sealed class BinanceRestClient : IBinanceRestClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceRestClient> _logger;
    private readonly BinanceSettings _settings;

    // Simple token-bucket rate limiter: track request timestamps
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private readonly Queue<DateTimeOffset> _requestTimestamps = new();

    public BinanceRestClient(
        HttpClient httpClient,
        ILogger<BinanceRestClient> logger,
        IOptions<BinanceSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<List<JsonElement>> GetKlinesAsync(string symbol, string interval, int limit = 500, CancellationToken cancellationToken = default)
    {
        await EnforceRateLimitAsync(cancellationToken);

        var url = $"/api/v3/klines?symbol={symbol.ToUpperInvariant()}&interval={interval}&limit={limit}";
        _logger.LogDebug("Fetching klines: {Url}", url);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var klines = JsonSerializer.Deserialize<List<JsonElement>>(content) ?? [];

        _logger.LogDebug("Received {Count} klines for {Symbol} ({Interval})", klines.Count, symbol, interval);
        return klines;
    }

    public async Task<decimal> GetTickerPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        await EnforceRateLimitAsync(cancellationToken);

        var url = $"/api/v3/ticker/price?symbol={symbol.ToUpperInvariant()}";
        _logger.LogDebug("Fetching ticker price: {Url}", url);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var priceStr = doc.RootElement.GetProperty("price").GetString()
            ?? throw new InvalidOperationException($"No price returned for {symbol}");

        return decimal.Parse(priceStr, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<JsonElement> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.ApiSecret))
            throw new InvalidOperationException("Binance API key and secret are required for authenticated endpoints");

        await EnforceRateLimitAsync(cancellationToken);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"timestamp={timestamp}";
        var signature = CreateHmacSignature(queryString);

        var url = $"/api/v3/account?{queryString}&signature={signature}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-MBX-APIKEY", _settings.ApiKey);

        _logger.LogDebug("Fetching account info (signed request)");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        return doc.RootElement.Clone();
    }

    private string CreateHmacSignature(string queryString)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.ApiSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimitSemaphore.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now.AddMinutes(-1);

            // Remove timestamps outside the 1-minute window
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
                _requestTimestamps.Dequeue();

            if (_requestTimestamps.Count >= ExchangeConstants.MaxWeightPerMinute)
            {
                var oldestInWindow = _requestTimestamps.Peek();
                var waitTime = oldestInWindow.AddMinutes(1) - now;

                if (waitTime > TimeSpan.Zero)
                {
                    _logger.LogWarning("Rate limit approaching ({Count}/{Max}). Waiting {Wait}ms",
                        _requestTimestamps.Count, ExchangeConstants.MaxWeightPerMinute, waitTime.TotalMilliseconds);
                    await Task.Delay(waitTime, cancellationToken);
                }
            }

            _requestTimestamps.Enqueue(now);
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }
}
