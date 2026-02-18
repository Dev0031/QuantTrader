using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantTrader.ApiGateway.Services;
using QuantTrader.ApiGateway.Workers;
using StackExchange.Redis;

namespace QuantTrader.ApiGateway.Controllers;

/// <summary>
/// Provides system transparency and diagnostics endpoints for the dashboard.
/// Includes pipeline health, live activity log, and simulation mode control.
/// </summary>
[ApiController]
[Route("api/system")]
[AllowAnonymous]
public sealed class SystemController : ControllerBase
{
    private const string ActivityKey = "system:activity:log";
    private const string SimEnabledKey = "system:simulation:enabled";
    private const string SimStatsKey = "system:simulation:stats";
    private const string HeartbeatPrefix = "service:heartbeat:";
    private const string PricePrefix = "price:latest:";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDatabase _redis;
    private readonly IActivityLogService _activity;
    private readonly ILogger<SystemController> _logger;

    private static readonly string[] WatchedSymbols = ["BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT"];

    private static readonly string[] KnownServices =
    [
        "ApiGateway", "DataIngestion", "StrategyEngine", "RiskManager", "ExecutionEngine"
    ];

    public SystemController(
        IConnectionMultiplexer redis,
        IActivityLogService activity,
        ILogger<SystemController> logger)
    {
        _redis = redis.GetDatabase();
        _activity = activity;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /api/system/activity
    // -------------------------------------------------------------------------
    /// <summary>Returns the most recent activity log entries across all services.</summary>
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity(
        [FromQuery] int limit = 100,
        [FromQuery] string? service = null,
        [FromQuery] string? level = null,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 500) limit = 100;

        var raw = await _redis.ListRangeAsync(ActivityKey, 0, 499);
        var entries = new List<ActivityEntry>(raw.Length);

        foreach (var item in raw)
        {
            if (item.IsNullOrEmpty) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<ActivityEntry>(item!, JsonOpts);
                if (entry is null) continue;

                if (service is not null && !entry.Service.Equals(service, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (level is not null && !entry.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                    continue;

                entries.Add(entry);
            }
            catch { /* skip malformed entries */ }
        }

        return Ok(entries.Take(limit));
    }

    // -------------------------------------------------------------------------
    // GET /api/system/pipeline
    // -------------------------------------------------------------------------
    /// <summary>Returns the health status of each stage in the data pipeline.</summary>
    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipeline(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var stages = new List<PipelineStageDto>();

        // Stage 1: Binance Connection — determined by freshness of price Redis keys
        var btcKey = await _redis.KeyTimeToLiveAsync($"{PricePrefix}BTCUSDT");
        var binanceActive = btcKey is not null && btcKey > TimeSpan.Zero;
        var btcPrice = await _redis.StringGetAsync($"{PricePrefix}BTCUSDT");
        stages.Add(new PipelineStageDto(
            Name: "Binance WebSocket",
            Service: "DataIngestion",
            Status: binanceActive ? "active" : "idle",
            LastActivityAt: binanceActive ? now.ToString("o") : null,
            LastMessage: binanceActive ? $"BTCUSDT @ ${(decimal.TryParse(btcPrice, out var p) ? p.ToString("N2") : "?")}" : "No price data in Redis",
            Icon: "exchange"));

        // Stage 2: DataIngestion service heartbeat
        var diHb = await ReadHeartbeatAsync("DataIngestion");
        stages.Add(new PipelineStageDto(
            Name: "DataIngestion",
            Service: "DataIngestion",
            Status: HeartbeatStatus(diHb, now),
            LastActivityAt: diHb?.LastHeartbeat,
            LastMessage: diHb?.CurrentActivity ?? (binanceActive ? "Streaming & writing to Redis" : "Service not reporting"),
            Icon: "database"));

        // Stage 3: Event Bus / Bridge
        var simEnabled = await _redis.StringGetAsync(SimEnabledKey) == "1";
        stages.Add(new PipelineStageDto(
            Name: "Event Bus",
            Service: "ApiGateway",
            Status: binanceActive || simEnabled ? "active" : "idle",
            LastActivityAt: binanceActive || simEnabled ? now.ToString("o") : null,
            LastMessage: simEnabled ? "Simulation events flowing" : (binanceActive ? "Redis bridge forwarding ticks" : "Waiting for data source"),
            Icon: "zap"));

        // Stage 4: StrategyEngine
        var seHb = await ReadHeartbeatAsync("StrategyEngine");
        stages.Add(new PipelineStageDto(
            Name: "StrategyEngine",
            Service: "StrategyEngine",
            Status: HeartbeatStatus(seHb, now),
            LastActivityAt: seHb?.LastHeartbeat,
            LastMessage: seHb?.CurrentActivity ?? "Service not running",
            Icon: "brain"));

        // Stage 5: RiskManager
        var rmHb = await ReadHeartbeatAsync("RiskManager");
        stages.Add(new PipelineStageDto(
            Name: "RiskManager",
            Service: "RiskManager",
            Status: HeartbeatStatus(rmHb, now),
            LastActivityAt: rmHb?.LastHeartbeat,
            LastMessage: rmHb?.CurrentActivity ?? "Service not running",
            Icon: "shield"));

        // Stage 6: ExecutionEngine
        var eeHb = await ReadHeartbeatAsync("ExecutionEngine");
        stages.Add(new PipelineStageDto(
            Name: "ExecutionEngine",
            Service: "ExecutionEngine",
            Status: HeartbeatStatus(eeHb, now),
            LastActivityAt: eeHb?.LastHeartbeat,
            LastMessage: eeHb?.CurrentActivity ?? "Service not running",
            Icon: "send"));

        // Stage 7: SignalR → Dashboard
        stages.Add(new PipelineStageDto(
            Name: "Dashboard (SignalR)",
            Service: "ApiGateway",
            Status: "active",   // ApiGateway itself is running (we're handling this request)
            LastActivityAt: now.ToString("o"),
            LastMessage: "API Gateway ready",
            Icon: "monitor"));

        return Ok(stages);
    }

    // -------------------------------------------------------------------------
    // GET /api/system/simulation
    // -------------------------------------------------------------------------
    /// <summary>Returns current simulation mode status and statistics.</summary>
    [HttpGet("simulation")]
    public async Task<IActionResult> GetSimulation(CancellationToken ct = default)
    {
        var enabled = await _redis.StringGetAsync(SimEnabledKey) == "1";
        var statsJson = await _redis.StringGetAsync(SimStatsKey);

        if (!statsJson.IsNullOrEmpty)
        {
            try
            {
                var stats = JsonSerializer.Deserialize<object>(statsJson!, JsonOpts);
                return Ok(stats);
            }
            catch { /* fall through */ }
        }

        return Ok(new { enabled, startedAt = (string?)null, ticksGenerated = 0L, symbols = Array.Empty<string>() });
    }

    // -------------------------------------------------------------------------
    // POST /api/system/simulation/start
    // -------------------------------------------------------------------------
    /// <summary>Enables simulation mode. Generates synthetic market ticks for testing.</summary>
    [HttpPost("simulation/start")]
    public async Task<IActionResult> StartSimulation(CancellationToken ct = default)
    {
        await _redis.StringSetAsync(SimEnabledKey, "1", TimeSpan.FromHours(24));
        await _activity.LogAsync("System", "success",
            "Simulation mode enabled via dashboard", ct: ct);
        _logger.LogInformation("Simulation mode enabled");
        return Ok(new { message = "Simulation started. Synthetic market ticks will be generated." });
    }

    // -------------------------------------------------------------------------
    // POST /api/system/simulation/stop
    // -------------------------------------------------------------------------
    /// <summary>Disables simulation mode.</summary>
    [HttpPost("simulation/stop")]
    public async Task<IActionResult> StopSimulation(CancellationToken ct = default)
    {
        await _redis.KeyDeleteAsync(SimEnabledKey);
        await _redis.KeyDeleteAsync(SimStatsKey);
        await _activity.LogAsync("System", "info",
            "Simulation mode disabled via dashboard", ct: ct);
        _logger.LogInformation("Simulation mode disabled");
        return Ok(new { message = "Simulation stopped." });
    }

    // -------------------------------------------------------------------------
    // POST /api/system/diagnose
    // -------------------------------------------------------------------------
    /// <summary>Runs a quick diagnostic and returns actionable issues.</summary>
    [HttpPost("diagnose")]
    public async Task<IActionResult> Diagnose(CancellationToken ct = default)
    {
        var issues = new List<DiagnosticIssue>();
        var tips = new List<string>();

        // Check Redis
        try
        {
            await _redis.PingAsync();
        }
        catch
        {
            issues.Add(new("error", "Redis", "Redis is not reachable",
                ["Ensure Redis/Docker is running: docker compose up -d redis"]));
        }

        // Check Binance data in Redis
        var btcPrice = await _redis.StringGetAsync($"{PricePrefix}BTCUSDT");
        if (btcPrice.IsNullOrEmpty)
        {
            issues.Add(new("warning", "DataIngestion",
                "No Binance price data in Redis for BTCUSDT",
                [
                    "Start the DataIngestion service: dotnet run --project src/Services/DataIngestion",
                    "Or enable Simulation Mode to generate test data without a real Binance connection",
                    "Check DataIngestion logs for WebSocket connection errors"
                ]));
        }

        // Check simulation
        var simEnabled = await _redis.StringGetAsync(SimEnabledKey) == "1";
        if (btcPrice.IsNullOrEmpty && !simEnabled)
        {
            tips.Add("Enable Simulation Mode to test the full pipeline without a live exchange connection.");
        }

        // Check API key configured
        var exchangeSettingsJson = await _redis.StringGetAsync("settings:exchange");
        if (exchangeSettingsJson.IsNullOrEmpty)
        {
            issues.Add(new("info", "Settings",
                "No exchange API keys configured",
                [
                    "Go to Settings page and add your Binance testnet API key",
                    "Note: The DataIngestion service reads its key from appsettings.json or environment variables at startup",
                    "Set BINANCE__APIKEY and BINANCE__APISECRET environment variables for DataIngestion"
                ]));
        }

        await _activity.LogAsync("System", issues.Count == 0 ? "success" : "warning",
            $"Diagnostic complete: {issues.Count} issue(s) found", ct: ct);

        return Ok(new { issues, tips, timestamp = DateTimeOffset.UtcNow });
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<HeartbeatRecord?> ReadHeartbeatAsync(string serviceName)
    {
        try
        {
            var json = await _redis.StringGetAsync($"{HeartbeatPrefix}{serviceName}");
            if (json.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<HeartbeatRecord>(json!, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static string HeartbeatStatus(HeartbeatRecord? hb, DateTimeOffset now)
    {
        if (hb is null) return "unknown";
        if (!DateTimeOffset.TryParse(hb.LastHeartbeat, out var lastHb)) return "unknown";
        var age = now - lastHb;
        return age.TotalMinutes < 2 ? "active"
             : age.TotalMinutes < 10 ? "idle"
             : "error";
    }

    // -------------------------------------------------------------------------
    // DTOs
    // -------------------------------------------------------------------------

    private sealed record PipelineStageDto(
        string Name,
        string Service,
        string Status,
        string? LastActivityAt,
        string? LastMessage,
        string Icon);

    private sealed record HeartbeatRecord(
        string ServiceName,
        string Status,
        string LastHeartbeat,
        long EventsProcessed,
        string? CurrentActivity);

    private sealed record DiagnosticIssue(
        string Severity,
        string Component,
        string Message,
        string[] Steps);
}
