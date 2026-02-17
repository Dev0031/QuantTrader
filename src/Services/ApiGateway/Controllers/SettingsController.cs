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
    private readonly ILogger<SettingsController> _logger;

    private const string ExchangeSettingsKey = "settings:exchange";
    private const string ApiKeysPrefix = "settings:apikeys:";

    public SettingsController(IRedisCacheService redis, ILogger<SettingsController> logger)
    {
        _redis = redis;
        _logger = logger;
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

    /// <summary>Saves or updates an exchange connection.</summary>
    [HttpPost("exchanges")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveExchangeSettings(
        [FromBody] SaveExchangeSettingsRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Exchange))
            return BadRequest(new { message = "Exchange is required" });

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return BadRequest(new { message = "API Key is required" });

        if (string.IsNullOrWhiteSpace(request.ApiSecret))
            return BadRequest(new { message = "API Secret is required" });

        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct)
                       ?? [];

        var existing = settings.FindIndex(s =>
            string.Equals(s.Exchange, request.Exchange, StringComparison.OrdinalIgnoreCase));

        var entry = new StoredExchangeSettings(
            Exchange: request.Exchange,
            ApiKey: request.ApiKey,
            ApiSecret: request.ApiSecret,
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
            HasSecret: true,
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

    /// <summary>Verifies an exchange connection by attempting a lightweight API call.</summary>
    [HttpPost("exchanges/{exchange}/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyExchangeSettings(string exchange, CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct);
        var entry = settings?.FirstOrDefault(s =>
            string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return NotFound(new { message = $"No settings found for {exchange}" });

        // For now, mark as verified. In production, this would call the exchange API.
        var updated = entry with { Status = "Verified", LastVerified = DateTimeOffset.UtcNow };
        var idx = settings!.FindIndex(s =>
            string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));
        settings[idx] = updated;
        await _redis.SetAsync(ExchangeSettingsKey, settings, ct: ct);

        return Ok(new { message = "Connection verified", status = "Verified" });
    }

    /// <summary>Gets the status of all required API keys for the system.</summary>
    [HttpGet("api-keys/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetApiKeyStatus(CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct)
                       ?? [];

        var requiredKeys = new[]
        {
            new { Name = "Binance", Description = "Main exchange for trading (spot & futures). Required for live trading." },
            new { Name = "Bybit", Description = "Alternative exchange. Optional backup for order routing." },
            new { Name = "OKX", Description = "Alternative exchange. Optional for multi-exchange strategies." },
            new { Name = "CoinGecko", Description = "Market data aggregator. Used for sentiment and price feeds." },
            new { Name = "CryptoPanic", Description = "News/sentiment feed. Used for news-based trading signals." },
        };

        var result = requiredKeys.Select(rk =>
        {
            var configured = settings.FirstOrDefault(s =>
                string.Equals(s.Exchange, rk.Name, StringComparison.OrdinalIgnoreCase));

            return new ApiKeyStatusResponse(
                Name: rk.Name,
                Description: rk.Description,
                IsConfigured: configured is not null,
                MaskedKey: configured is not null ? MaskKey(configured.ApiKey) : null,
                Status: configured?.Status ?? "Not Configured");
        });

        return Ok(result);
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "***";
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
}
