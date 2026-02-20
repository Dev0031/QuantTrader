using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantTrader.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace QuantTrader.TestInfrastructure.Fixtures;

/// <summary>
/// Starts a real TimescaleDB/PostgreSQL container, runs EF migrations, and exposes
/// a factory for creating <see cref="TradingDbContext"/> instances.
/// Decorated with [CollectionDefinition] so tests sharing this fixture share one container.
/// </summary>
[CollectionDefinition("PostgreSql")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture> { }

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:latest-pg16")
        .WithDatabase("quanttrader_test")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Run EF migrations
        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new TradingDbContext(options);
        await context.Database.MigrateAsync();
    }

    public TradingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new TradingDbContext(options);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
