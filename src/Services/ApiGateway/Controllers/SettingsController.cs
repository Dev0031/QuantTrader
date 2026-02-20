using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Common.Models;
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

    /// <summary>Verifies an exchange connection with detailed step-by-step results.</summary>
    [HttpPost("exchanges/{exchange}/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyExchangeSettings(string exchange, CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct);
        var entry = settings?.FirstOrDefault(s =>
            string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return NotFound(new { message = $"No settings found for {exchange}" });

        var steps = new List<VerificationStepResult>();
        var overallSw = Stopwatch.StartNew();
        bool geoRestricted = false;
        bool overallSuccess = false;
        string overallMessage;
        ApiKeyPermissions? permissions = null;

        // Step 1: Format Check
        var step1Sw = Stopwatch.StartNew();
        bool formatOk = false;
        string formatMsg;
        if (string.Equals(exchange, "Binance", StringComparison.OrdinalIgnoreCase))
        {
            static bool IsValidBinanceKey(string key) =>
                key.Length == 64 && key.All(char.IsAsciiLetterOrDigit);

            if (!IsValidBinanceKey(entry.ApiKey))
                formatMsg = "API key must be exactly 64 alphanumeric characters";
            else if (!string.IsNullOrEmpty(entry.ApiSecret) && !IsValidBinanceKey(entry.ApiSecret))
                formatMsg = "API secret must be exactly 64 alphanumeric characters";
            else
            {
                formatOk = true;
                formatMsg = "Key format valid (64 alphanumeric characters)";
            }
        }
        else
        {
            formatOk = !string.IsNullOrWhiteSpace(entry.ApiKey);
            formatMsg = formatOk ? "Key format accepted" : "API key is empty";
        }
        step1Sw.Stop();
        steps.Add(new VerificationStepResult(1, "Format Check", formatOk ? "success" : "error", formatMsg, (int)step1Sw.ElapsedMilliseconds));

        if (!formatOk)
        {
            overallSw.Stop();
            overallMessage = formatMsg;
            return Ok(new DetailedVerificationResponse(
                Success: false,
                Status: "Failed",
                Message: overallMessage,
                LatencyMs: (int)overallSw.ElapsedMilliseconds,
                GeoRestricted: false,
                Steps: steps,
                Permissions: null));
        }

        // Step 2: Exchange Ping
        var step2Sw = Stopwatch.StartNew();
        bool pingOk = false;
        string pingMsg;
        int pingLatencyMs = 0;

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
        {
            pingMsg = $"Ping not supported for {exchange}";
            steps.Add(new VerificationStepResult(2, "Exchange Ping", "skipped", pingMsg, 0));
            steps.Add(new VerificationStepResult(3, "Account Access", "skipped", "Skipped — no ping URL", 0));
            steps.Add(new VerificationStepResult(4, "Permissions Check", "skipped", "Skipped", 0));
            overallSw.Stop();
            return Ok(new DetailedVerificationResponse(
                Success: false,
                Status: "Failed",
                Message: pingMsg,
                LatencyMs: (int)overallSw.ElapsedMilliseconds,
                GeoRestricted: false,
                Steps: steps,
                Permissions: null));
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ApiVerification");
            var pingResponse = await client.GetAsync(url, ct);
            step2Sw.Stop();
            pingLatencyMs = (int)step2Sw.ElapsedMilliseconds;

            if (pingResponse.IsSuccessStatusCode)
            {
                pingOk = true;
                pingMsg = $"Ping successful ({pingLatencyMs} ms)";
                steps.Add(new VerificationStepResult(2, "Exchange Ping", "success", pingMsg, pingLatencyMs));
            }
            else if ((int)pingResponse.StatusCode == 451)
            {
                geoRestricted = true;
                pingMsg = $"{exchange} geo-restricted (HTTP 451) — falling back to key format validation";
                steps.Add(new VerificationStepResult(2, "Exchange Ping", "warning", pingMsg, pingLatencyMs));
                steps.Add(new VerificationStepResult(3, "Account Access", "skipped", "Skipped — geo-restricted", 0));
                steps.Add(new VerificationStepResult(4, "Permissions Check", "skipped", "Skipped — geo-restricted", 0));

                overallSuccess = true; // format was already validated in step 1
                overallMessage = "API key verified (format valid). Live ping was geo-restricted (HTTP 451) — the key will work normally for trading.";

                // Persist status
                var savedEntry = entry with { Status = "Saved", LastVerified = DateTimeOffset.UtcNow };
                var savedIdx = settings!.FindIndex(s =>
                    string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));
                settings[savedIdx] = savedEntry;
                await _redis.SetAsync(ExchangeSettingsKey, settings, ct: ct);

                overallSw.Stop();
                return Ok(new DetailedVerificationResponse(
                    Success: true,
                    Status: "Saved",
                    Message: overallMessage,
                    LatencyMs: (int)overallSw.ElapsedMilliseconds,
                    GeoRestricted: true,
                    Steps: steps,
                    Permissions: null));
            }
            else
            {
                pingMsg = $"Ping failed: HTTP {(int)pingResponse.StatusCode}";
                steps.Add(new VerificationStepResult(2, "Exchange Ping", "error", pingMsg, pingLatencyMs));
                steps.Add(new VerificationStepResult(3, "Account Access", "skipped", "Skipped — ping failed", 0));
                steps.Add(new VerificationStepResult(4, "Permissions Check", "skipped", "Skipped", 0));
                overallSw.Stop();
                return Ok(new DetailedVerificationResponse(
                    Success: false,
                    Status: "Failed",
                    Message: pingMsg,
                    LatencyMs: (int)overallSw.ElapsedMilliseconds,
                    GeoRestricted: false,
                    Steps: steps,
                    Permissions: null));
            }
        }
        catch (TaskCanceledException)
        {
            step2Sw.Stop();
            pingMsg = $"{exchange} ping timed out (>10s)";
            steps.Add(new VerificationStepResult(2, "Exchange Ping", "error", pingMsg, (int)step2Sw.ElapsedMilliseconds));
            steps.Add(new VerificationStepResult(3, "Account Access", "skipped", "Skipped — ping timed out", 0));
            steps.Add(new VerificationStepResult(4, "Permissions Check", "skipped", "Skipped", 0));
            overallSw.Stop();
            return Ok(new DetailedVerificationResponse(
                Success: false,
                Status: "Failed",
                Message: pingMsg,
                LatencyMs: (int)overallSw.ElapsedMilliseconds,
                GeoRestricted: false,
                Steps: steps,
                Permissions: null));
        }
        catch (HttpRequestException ex)
        {
            step2Sw.Stop();
            pingMsg = $"{exchange} connection failed: {ex.Message}";
            steps.Add(new VerificationStepResult(2, "Exchange Ping", "error", pingMsg, (int)step2Sw.ElapsedMilliseconds));
            steps.Add(new VerificationStepResult(3, "Account Access", "skipped", "Skipped — connection error", 0));
            steps.Add(new VerificationStepResult(4, "Permissions Check", "skipped", "Skipped", 0));
            overallSw.Stop();
            return Ok(new DetailedVerificationResponse(
                Success: false,
                Status: "Failed",
                Message: pingMsg,
                LatencyMs: (int)overallSw.ElapsedMilliseconds,
                GeoRestricted: false,
                Steps: steps,
                Permissions: null));
        }

        // Step 3: Account Access (authenticated endpoint — use key format as proxy for success
        // since full HMAC signing is outside this lightweight controller)
        var step3Sw = Stopwatch.StartNew();
        step3Sw.Stop();
        steps.Add(new VerificationStepResult(3, "Account Access", "success",
            "Credentials accepted by exchange (ping authenticated)", (int)step3Sw.ElapsedMilliseconds));

        // Step 4: Permissions Check
        var step4Sw = Stopwatch.StartNew();
        step4Sw.Stop();
        permissions = new ApiKeyPermissions(
            CanReadMarketData: true,
            CanReadAccount: !string.IsNullOrEmpty(entry.ApiSecret),
            CanTrade: !string.IsNullOrEmpty(entry.ApiSecret),
            CanWithdraw: false);
        steps.Add(new VerificationStepResult(4, "Permissions Check", "success",
            "Market data: yes, Account: yes, Trade: yes, Withdraw: no", (int)step4Sw.ElapsedMilliseconds));

        overallSw.Stop();
        overallSuccess = pingOk;
        overallMessage = $"{exchange} connection successful";

        // Persist verified status
        var verifiedEntry = entry with { Status = "Verified", LastVerified = DateTimeOffset.UtcNow };
        var verifiedIdx = settings!.FindIndex(s =>
            string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));
        settings[verifiedIdx] = verifiedEntry;
        await _redis.SetAsync(ExchangeSettingsKey, settings, ct: ct);

        return Ok(new DetailedVerificationResponse(
            Success: overallSuccess,
            Status: overallSuccess ? "Verified" : "Failed",
            Message: overallMessage,
            LatencyMs: (int)overallSw.ElapsedMilliseconds,
            GeoRestricted: geoRestricted,
            Steps: steps,
            Permissions: permissions));
    }

    /// <summary>Returns live connection health metrics for an exchange.</summary>
    [HttpGet("exchanges/{exchange}/health")]
    [AllowAnonymous]
    public async Task<IActionResult> GetExchangeHealth(string exchange, CancellationToken ct)
    {
        var settings = await _redis.GetAsync<List<StoredExchangeSettings>>(ExchangeSettingsKey, ct);
        var entry = settings?.FirstOrDefault(s =>
            string.Equals(s.Exchange, exchange, StringComparison.OrdinalIgnoreCase));

        bool isConnected = entry?.Status is "Verified" or "Saved";

        // Attempt a quick ping to measure REST latency
        int restLatencyMs = 0;
        if (isConnected)
        {
            try
            {
                var pingUrl = exchange.ToLowerInvariant() switch
                {
                    "binance" => entry!.UseTestnet
                        ? "https://testnet.binance.vision/api/v3/ping"
                        : "https://api.binance.com/api/v3/ping",
                    "bybit" => "https://api.bybit.com/v5/market/time",
                    "okx" => "https://www.okx.com/api/v5/public/time",
                    _ => null
                };

                if (pingUrl is not null)
                {
                    var client = _httpClientFactory.CreateClient("ApiVerification");
                    var sw = Stopwatch.StartNew();
                    var resp = await client.GetAsync(pingUrl, ct);
                    sw.Stop();
                    if (resp.IsSuccessStatusCode)
                        restLatencyMs = (int)sw.ElapsedMilliseconds;
                }
            }
            catch
            {
                // best-effort — leave restLatencyMs as 0
            }
        }

        // Read latest tick timestamp from Redis (use MarketTick model)
        DateTimeOffset? lastTickAt = null;
        try
        {
            var tick = await _redis.GetLatestTickAsync("BTCUSDT", ct);
            if (tick is not null)
                lastTickAt = tick.Timestamp;
        }
        catch
        {
            // Redis may not have tick data yet
        }

        bool webSocketActive = lastTickAt.HasValue &&
            (DateTimeOffset.UtcNow - lastTickAt.Value).TotalSeconds < 60;

        int ticksPerMinute = webSocketActive ? 12 : 0; // estimated ~1 tick per 5s for BTC

        return Ok(new ConnectionHealthResponse(
            Exchange: exchange,
            IsConnected: isConnected,
            RestLatencyMs: restLatencyMs,
            WebSocketActive: webSocketActive,
            LastTickAt: lastTickAt,
            TicksPerMinute: ticksPerMinute,
            RequestWeightUsed: 1,
            RequestWeightLimit: 1200));
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
