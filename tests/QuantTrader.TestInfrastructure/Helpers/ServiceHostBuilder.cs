using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Services;
using QuantTrader.Infrastructure.Extensions;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.TestInfrastructure.Fakes;

namespace QuantTrader.TestInfrastructure.Helpers;

/// <summary>
/// Builds a minimal DI container for component/integration tests.
/// Wires real services with fake adapters — no running Docker containers needed for component tests.
/// </summary>
public sealed class ServiceHostBuilder
{
    private readonly IServiceCollection _services = new ServiceCollection();

    public ServiceHostBuilder()
    {
        _services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _services.AddSingleton<IEventBus, FakeEventBus>();
        _services.AddSingleton<ITimeProvider, FakeTimeProvider>();

        // Default Paper mode settings
        _services.Configure<TradingModeSettings>(opts =>
        {
            opts.Mode = Common.Enums.TradingMode.Paper;
            opts.PaperFillLatencyMs = 0;
        });
    }

    public ServiceHostBuilder WithRealRedis(string connectionString)
    {
        _services.AddRedisCache(connectionString);
        return this;
    }

    public ServiceHostBuilder WithFakeRedis()
    {
        _services.AddSingleton<IRedisCacheService>(new FakeRedisCacheService());
        return this;
    }

    public ServiceHostBuilder Add<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        _services.AddSingleton<TInterface, TImplementation>();
        return this;
    }

    public ServiceHostBuilder Configure<TOptions>(Action<TOptions> configure) where TOptions : class
    {
        _services.Configure(configure);
        return this;
    }

    public IServiceProvider Build() => _services.BuildServiceProvider();
}

/// <summary>Minimal no-op Redis service for tests that don't need real caching.</summary>
public sealed class FakeRedisCacheService : IRedisCacheService
{
    private readonly Dictionary<string, string> _store = new();

    public void Clear() => _store.Clear();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (_store.TryGetValue(key, out var json))
            return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json));
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        _store[key] = System.Text.Json.JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }

    public Task<Common.Models.MarketTick?> GetLatestTickAsync(string symbol, CancellationToken ct = default)
        => GetAsync<Common.Models.MarketTick>($"tick:latest:{symbol.ToUpperInvariant()}", ct);

    public Task SetLatestTickAsync(string symbol, Common.Models.MarketTick tick, CancellationToken ct = default)
        => SetAsync($"tick:latest:{symbol.ToUpperInvariant()}", tick, null, ct);

    public Task<Common.Models.PortfolioSnapshot?> GetPortfolioSnapshotAsync(CancellationToken ct = default)
        => GetAsync<Common.Models.PortfolioSnapshot>("portfolio:snapshot", ct);

    public Task SetPortfolioSnapshotAsync(Common.Models.PortfolioSnapshot snapshot, CancellationToken ct = default)
        => SetAsync("portfolio:snapshot", snapshot, null, ct);
}
