using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuantTrader.Infrastructure.Database;

namespace QuantTrader.Infrastructure.HealthChecks;

/// <summary>Health check that verifies connectivity to the PostgreSQL database.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly TradingDbContext _dbContext;

    public DatabaseHealthCheck(TradingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy("Database connection is healthy.")
                : HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
