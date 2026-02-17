using System.Text.Json;
using Microsoft.Extensions.Options;

namespace QuantTrader.DataIngestion.Clients;

/// <summary>Client for the CryptoPanic news aggregation API.</summary>
public interface ICryptoPanicClient
{
    /// <summary>Get latest crypto news items with optional filter (rising, hot, bullish, bearish, important, lol).</summary>
    Task<List<CryptoPanicNewsItem>> GetLatestNewsAsync(string? filter = null, CancellationToken cancellationToken = default);
}

/// <summary>A single news item from CryptoPanic.</summary>
public sealed record CryptoPanicNewsItem(
    long Id,
    string Title,
    string Url,
    string? Sentiment,
    DateTimeOffset PublishedAt,
    string Source);

public sealed class CryptoPanicClient : ICryptoPanicClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CryptoPanicClient> _logger;
    private readonly CryptoPanicSettings _settings;

    public CryptoPanicClient(
        HttpClient httpClient,
        ILogger<CryptoPanicClient> logger,
        IOptions<CryptoPanicSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<List<CryptoPanicNewsItem>> GetLatestNewsAsync(string? filter = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogDebug("CryptoPanic API key not configured. Returning empty results");
            return [];
        }

        var url = $"/posts/?auth_token={_settings.ApiKey}&currencies=BTC,ETH,BNB,SOL,XRP&kind=news";
        if (!string.IsNullOrWhiteSpace(filter))
        {
            url += $"&filter={filter}";
        }

        _logger.LogDebug("CryptoPanic news query: filter={Filter}", filter ?? "none");

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var results = new List<CryptoPanicNewsItem>();

        if (!doc.RootElement.TryGetProperty("results", out var resultsElement))
            return results;

        foreach (var item in resultsElement.EnumerateArray())
        {
            try
            {
                var newsItem = new CryptoPanicNewsItem(
                    Id: item.GetProperty("id").GetInt64(),
                    Title: item.GetProperty("title").GetString() ?? string.Empty,
                    Url: item.GetProperty("url").GetString() ?? string.Empty,
                    Sentiment: item.TryGetProperty("votes", out var votes)
                        ? DetermineSentiment(votes)
                        : null,
                    PublishedAt: item.GetProperty("published_at").GetDateTimeOffset(),
                    Source: item.TryGetProperty("source", out var source) && source.TryGetProperty("title", out var sourceTitle)
                        ? sourceTitle.GetString() ?? "Unknown"
                        : "Unknown");

                results.Add(newsItem);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse CryptoPanic news item");
            }
        }

        return results;
    }

    private static string? DetermineSentiment(JsonElement votes)
    {
        var positive = votes.TryGetProperty("positive", out var pos) ? pos.GetInt32() : 0;
        var negative = votes.TryGetProperty("negative", out var neg) ? neg.GetInt32() : 0;

        if (positive == 0 && negative == 0)
            return null;

        return positive > negative ? "bullish" : negative > positive ? "bearish" : "neutral";
    }
}
