using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.TestInfrastructure.Helpers;
using StackExchange.Redis;
using Xunit;

namespace QuantTrader.ApiGateway.Tests;

/// <summary>
/// Single shared WebApplicationFactory for all ApiGateway tests.
/// Using ICollectionFixture ensures only ONE host is started per test run.
/// Background hosted services are removed so the test host does not try to
/// connect to Redis or other infrastructure not available in CI.
/// </summary>
public sealed class ApiGatewayFixture : WebApplicationFactory<Program>
{
    public FakeRedisCacheService Redis { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace IRedisCacheService with in-memory fake
            var redisDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IRedisCacheService));
            if (redisDesc is not null) services.Remove(redisDesc);
            services.AddSingleton<IRedisCacheService>(Redis);

            // Replace IConnectionMultiplexer (used by background workers / SignalR bridge)
            // to prevent real Redis connection attempts in CI
            var multiplexerDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (multiplexerDesc is not null) services.Remove(multiplexerDesc);
            services.AddSingleton(new Mock<IConnectionMultiplexer>().Object);

            // Remove all background services — they connect to Redis/ServiceBus which
            // are unavailable in CI and would crash the TestServer mid-run
            services.RemoveAll<IHostedService>();
        });
    }
}

[CollectionDefinition("ApiGateway")]
public class ApiGatewayCollection : ICollectionFixture<ApiGatewayFixture> { }
