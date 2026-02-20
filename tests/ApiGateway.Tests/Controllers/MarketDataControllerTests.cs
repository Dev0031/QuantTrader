using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.TestInfrastructure.Helpers;

namespace QuantTrader.ApiGateway.Tests.Controllers;

public sealed class MarketDataControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly FakeRedisCacheService _fakeRedis = new();

    public MarketDataControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRedisCacheService));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddSingleton<IRedisCacheService>(_fakeRedis);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetPrices_EndpointExists()
    {
        var response = await _client.GetAsync("/api/market/prices");
        // Should not return 404 (endpoint exists); may return 200 or other status
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }
}
