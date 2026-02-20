using StackExchange.Redis;
using Testcontainers.Redis;

namespace QuantTrader.TestInfrastructure.Fixtures;

/// <summary>
/// Starts a real Redis container and exposes an <see cref="IConnectionMultiplexer"/> for integration tests.
/// </summary>
[CollectionDefinition("Redis")]
public sealed class RedisCollection : ICollectionFixture<RedisFixture> { }

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public IConnectionMultiplexer Multiplexer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (Multiplexer is not null)
            await Multiplexer.DisposeAsync();
        await _container.DisposeAsync();
    }
}
