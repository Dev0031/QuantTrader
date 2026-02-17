using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.HealthChecks;
using QuantTrader.Infrastructure.KeyVault;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;
using StackExchange.Redis;

namespace QuantTrader.Infrastructure.Extensions;

/// <summary>Extension methods to register all QuantTrader.Infrastructure services into the DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="TradingDbContext"/> backed by PostgreSQL with Npgsql.
    /// </summary>
    public static IServiceCollection AddTradingDatabase(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<TradingDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
        });

        return services;
    }

    /// <summary>
    /// Registers Redis <see cref="IConnectionMultiplexer"/> and <see cref="IRedisCacheService"/>.
    /// </summary>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var configuration = ConfigurationOptions.Parse(connectionString);
            configuration.AbortOnConnectFail = false;
            configuration.ConnectRetry = 3;
            configuration.ConnectTimeout = 5000;
            return ConnectionMultiplexer.Connect(configuration);
        });

        services.AddSingleton<IRedisCacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IEventBus"/> implementation. Uses Azure Service Bus when
    /// "EventBus:ConnectionString" is configured; otherwise falls back to in-memory for local development.
    /// </summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration["EventBus:ConnectionString"];
        var useInMemory = string.IsNullOrWhiteSpace(connectionString)
                          || configuration.GetValue<bool>("EventBus:UseInMemory");

        if (useInMemory)
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        }
        else
        {
            services.AddSingleton(_ => new ServiceBusClient(connectionString));
            services.AddSingleton<IEventBus, ServiceBusEventBus>();
        }

        return services;
    }

    /// <summary>
    /// Registers <see cref="ISecretProvider"/> backed by Azure Key Vault with IConfiguration fallback.
    /// </summary>
    public static IServiceCollection AddKeyVaultSecrets(this IServiceCollection services, string? vaultUri = null)
    {
        services.AddSingleton<ISecretProvider>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KeyVaultService>>();

            Uri? uri = null;
            var configuredUri = vaultUri ?? config["KeyVault:VaultUri"];
            if (!string.IsNullOrWhiteSpace(configuredUri))
            {
                uri = new Uri(configuredUri);
            }

            return new KeyVaultService(config, logger, uri);
        });

        return services;
    }

    /// <summary>
    /// Configures OpenTelemetry tracing, metrics, and Prometheus exporter.
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services, string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddSource(serviceName);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
            });

        return services;
    }

    /// <summary>
    /// Registers infrastructure health checks for Redis and the trading database.
    /// </summary>
    public static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, tags: ["ready", "infra"])
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, tags: ["ready", "infra"]);

        return services;
    }
}
