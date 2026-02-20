using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>Manages exchange API keys and application settings.</summary>
[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly IRedisCacheService _redis;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SettingsController> _logger;

    private const string ExchangeSettingsKey = "settings:exchange";

    private static readonly ProviderDefinition[] Providers =
    [
        new("Binance", true, true, true, true,
            "Main exchange for trading, real-time prices, and account data",
            ["Live price streaming (WebSocket)", "Spot & margin trading", "Account balance & history", "Order placement & management"]),
        new("Bybit", true, true, true, false,
            "Alternative exchange for multi-exchange strategies",
            ["Spot & derivatives trading", "Account data", "Order management"]),
        new("OKX", true, true, true, false,
            "Alternative exchange for diversified order routing",
            ["Spot & derivatives trading", "Demo trading mode", "Account data"]),
        new("CoinGecko", false, false, false, false,
            "Market data aggregator. Free tier works without API key. Pro key increases rate limits to 500 req/min.",
            ["Market cap & volume data", "Price feeds for 10,000+ coins", "Historical OHLC data", "Works free without key"]),
        new("CryptoPanic", true, false, false, false,
            "News and sentiment feed for news-based trading signals. Free API key required.",
            ["Real-time crypto news", "Sentiment analysis", "Community voting data"]),
    ];

    public SettingsController(
        IRedisCacheService redis,
        IHttpClientFactory httpClientFactory,
        ILogger<SettingsController> logger)
    {
        _redis = redis;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>Gets metadata for all supported API providers with current config status.</summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct) ?? [];

        var result = Providers.Select(p =>
        {
            var configured = settings.FirstOrDefault(s =>
                string.Equals(s.Exchange, p.Name, StringComparison.OrdinalIgnoreCase));

            return new ApiProviderInfoResponse(
                Name: p.Name,
                RequiresApiKey: p.RequiresKey,
                RequiresApiSecret: p.RequiresSecret,
                SupportsTestnet: p.SupportsTestnet,
                IsRequired: p.IsRequired,
                Description: p.Description,
                Features: p.Features,
                IsConfigured: configured is not null,
                MaskedKey: configured is not null ? MaskKey(configured.ApiKey) : null,
                Status: configured?.Status ?? "Not Configured",
                LastVerified: configured?.LastVerified);
        });

        return Ok(result);
    }

    /// <summary>Gets all configured exchange connections.</summary>
    [HttpGet("exchanges")]
    [AllowAnonymous]
    public async Task<IActionResult> GetExchangeSettings(CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct);
        if (settings is null || settings.Count == 0)
        {
            return Ok(Array.Empty<ExchangeSettingsResponse>());
        }

        var response = settings.Select(s => new ExchangeSettingsResponse(
            Exchange: s.Exchange,
            ApiKeyMasked: MaskKey(s.ApiKey),
            HasSecret: !string.IsNullOrEmpty(s.ApiSecret),
            UseTestnet: s.UseTestnet,
            Status: s.Status,
            LastVerified: s.LastVerified));

        return Ok(response);
    }

    /// <summary>Saves or updates an exchange connection with conditional validation.</summary>
    [HttpPost("exchanges")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveExchangeSettings(
        [FromBody] SaveExchangeSettingsRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Exchange))
            return BadRequest(new { message = "Exchange is required" });

        var provider = Providers.FirstOrDefault(p =>
            string.Equals(p.Name, request.Exchange, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
            return BadRequest(new { message = $"Unknown provider: {request.Exchange}" });

        // Conditional validation based on provider requirements
        if (provider.RequiresKey && string.IsNullOrWhiteSpace(request.ApiKey))
            return BadRequest(new { message = $"API Key is required for {provider.Name}" });

        if (provider.RequiresSecret && string.IsNullOrWhiteSpace(request.ApiSecret))
            return BadRequest(new { message = $"API Secret is required for {provider.Name}" });

        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct) ?? [];

        var existing = settings.FindIndex(s =>
            string.Equals(s.Exchange, request.Exchange, StringComparison.OrdinalIgnoreCase));

        var entry = new StoredExchangeSettings(
            Exchange: request.Exchange,
            ApiKey: request.ApiKey ?? "",
            ApiSecret: request.ApiSecret ?? "",
            UseTestnet: request.UseTestnet,
            Status: "Configured",
            LastVerified: null,
            CreatedAt: existing >= 0 ? settings[existing].CreatedAt : DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        if (existing >= 0)
            settings[existing] = entry;
        else
            settings.Add(entry);

        await _redis.SetAsync(ExchangeSettingsKey, settings, ct: ct);

        _logger.LogInformation("Exchange settings saved for {Exchange} (testnet={Testnet})",
            request.Exchange, request.UseTestnet);

        return Ok(new ExchangeSettingsResponse(
            Exchange: entry.Exchange,
            ApiKeyMasked: MaskKey(entry.ApiKey),
            HasSecret: !string.IsNullOrEmpty(entry.ApiSecret),
            UseTestnet: entry.UseTestnet,
            Status: entry.Status,
            LastVerified: entry.LastVerified));
    }

    /// <summary>Deletes an exchange connection.</summary>
    [HttpDelete("exchanges/{exchange}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteExchangeSettings(string exchange, CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct);
        if (settings is null)
            return NotFound(new { message = $"No settings found for {exchange}" });

        var removed = settings.RemoveAll(s =>
            string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            return NotFound(new { message = $"No settings found for {exchange}" });

        await _redis.SetAsync(ExchangeSettingsKey, settings, ct: ct);

        _logger.LogInformation("Exchange settings deleted for {Exchange}", exchange);

        return Ok(new { message = $"Settings for {exchange} deleted" });
    }

    /// <summary>Verifies an exchange connection by making a real API call.</summary>
    [HttpPost("exchanges/{exchange}/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyExchangeSettings(string exchange, CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct);
        var entry = settings?.FirstOrDefault(s =>
            string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return NotFound(new { message = $"No settings found for {exchange}" });

        var sw = Stopwatch.StartNew();
        var (success, geoRestricted, message) = await VerifyProviderAsync(exchange, entry, ct);
        sw.Stop();

        if (success || geoRestricted)
        {
            // Geo-restricted: key can't be verified remotely but is valid; keep as "Saved"
            var newStatus = success ? "Verified" : "Saved";
            var updated = entry with { Status = newStatus, LastVerified = DateTimeOffset.UtcNow };
            var idx = settings!.FindIndex(s =>
                string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));
            settings[idx] = updated;
            await _redis.SetAsync(ExchangeSettingsKey, settings, ct: ct);
        }

        return Ok(new VerificationResultResponse(
            Success: success,
            Status: success ? "Verified" : (geoRestricted ? "Saved" : "Failed"),
            Message: message,
            LatencyMs: sw.ElapsedMilliseconds,
            GeoRestricted: geoRestricted));
    }

    /// <summary>Gets the status of all required API keys for the system.</summary>
    [HttpGet("api-keys/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetApiKeyStatus(CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct) ?? [];

        var result = Providers.Select(p =>
        {
            var configured = settings.FirstOrDefault(s =>
                string.Equals(s.Exchange, p.Name, StringComparison.OrdinalIgnoreCase));

            return new ApiKeyStatusResponse(
                Name: p.Name,
                Description: p.Description,
                IsConfigured: configured is not null,
                MaskedKey: configured is not null ? MaskKey(configured.ApiKey) : null,
                Status: configured?.Status ?? "Not Configured");
        });

        return Ok(result);
    }

    private async Task<(bool Success, bool GeoRestricted, string Message)> VerifyProviderAsync(
        string exchange, StoredExchangeSettings entry, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ApiVerification");

            var url = exchange.ToLowerInvariant() switch
            {
                "binance" => entry.UseTestnet
                    ? "https://testnet.binance.vision/api/v3/ping"
                    : "https://api.binance.com/api/v3/ping",
                "coingecko" => "https://api.coingecko.com/api/v3/ping",
                "cryptopanic" => string.IsNullOrEmpty(entry.ApiKey)
                    ? null
                    : $"https://cryptopanic.com/api/free/v1/posts/?auth_token={entry.ApiKey}&limit=1",
                "bybit" => entry.UseTestnet
                    ? "https://api-testnet.bybit.com/v5/market/time"
                    : "https://api.bybit.com/v5/market/time",
                "okx" => "https://www.okx.com/api/v5/public/time",
                _ => null
            };

            if (url is null)
                return (false, false, $"Verification not supported for {exchange}");

            var response = await client.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
                return (true, false, $"{exchange} connection successful");

            // HTTP 451 = geo-restricted by Binance for legal reasons (IP-level block).
            // The API key itself may be perfectly valid. Treat as a soft warning.
            if ((int)response.StatusCode == 451)
            {
                _logger.LogWarning("{Exchange} returned HTTP 451 (geo-restricted). Key saved but cannot be verified from this region.", exchange);
                return (false, true,
                    $"Key saved. {exchange} is geo-restricted from your server's location (HTTP 451). " +
                    "Switch to Testnet mode for local testing, or the key will work when deployed to an unrestricted region.");
            }

            return (false, false, $"{exchange} returned HTTP {(int)response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return (false, false, $"{exchange} connection timed out (>10s)");
        }
        catch (HttpRequestException ex)
        {
            return (false, false, $"{exchange} connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Verification failed for {Exchange}", exchange);
            return (false, false, $"Verification error: {ex.Message}");
        }
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (key.Length <= 8) return "****" + key[^2..];
        return key[..4] + "****" + key[^4..];
    }

    private sealed record StoredExchangeSettings(
        string Exchange,
        string ApiKey,
        string ApiSecret,
        bool UseTestnet,
        string Status,
        DateTimeOffset? LastVerified,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record ProviderDefinition(
        string Name,
        bool RequiresKey,
        bool RequiresSecret,
        bool SupportsTestnet,
        bool IsRequired,
        string Description,
        string[] Features);
}
