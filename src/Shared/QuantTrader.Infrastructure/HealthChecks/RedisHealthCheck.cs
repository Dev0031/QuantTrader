using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace QuantTrader.Infrastructure.HealthChecks;

/// <summary>Health check that verifies connectivity to Redis by issuing a PING command.</summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var latency = await db.PingAsync().ConfigureAwait(false);

            if (latency > TimeSpan.FromSeconds(2))
            {
                return HealthCheckResult.Degraded(
                    $"Redis responded but latency is high: {latency.TotalMilliseconds:F0}ms");
            }

            return HealthCheckResult.Healthy(
                $"Redis is healthy. Latency: {latency.TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", ex);
        }
    }
}
