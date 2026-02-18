using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using QuantTrader.ApiGateway.Hubs;
using StackExchange.Redis;

namespace QuantTrader.ApiGateway.Services;

/// <summary>
/// Centralised activity logger. Writes timestamped entries to a Redis capped list
/// and pushes them in real-time to connected dashboard clients via SignalR.
/// </summary>
public interface IActivityLogService
{
    Task LogAsync(string service, string level, string message,
        string? symbol = null, CancellationToken ct = default);
}

public sealed class ActivityLogService : IActivityLogService
{
    private const string RedisKey = "system:activity:log";
    private const long MaxEntries = 500;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDatabase _redis;
    private readonly IHubContext<TradingHub> _hub;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        IConnectionMultiplexer redis,
        IHubContext<TradingHub> hub,
        ILogger<ActivityLogService> logger)
    {
        _redis = redis.GetDatabase();
        _hub = hub;
        _logger = logger;
    }

    public async Task LogAsync(string service, string level, string message,
        string? symbol = null, CancellationToken ct = default)
    {
        var entry = new ActivityEntry(
            Id: Guid.NewGuid().ToString("N")[..12],
            Service: service,
            Level: level,
            Message: message,
            Symbol: symbol,
            Timestamp: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(entry, JsonOpts);

        try
        {
            // Push to Redis list and trim to keep it bounded
            await _redis.ListLeftPushAsync(RedisKey, json);
            await _redis.ListTrimAsync(RedisKey, 0, MaxEntries - 1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write activity entry to Redis");
        }

        // Push to all connected dashboard clients
        try
        {
            await _hub.Clients.Group("system")
                .SendAsync("OnSystemActivity", entry, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push activity entry via SignalR");
        }
    }
}

/// <summary>A single activity log entry.</summary>
public sealed record ActivityEntry(
    string Id,
    string Service,
    string Level,       // info | success | warning | error
    string Message,
    string? Symbol,
    DateTimeOffset Timestamp);
