using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using QuantTrader.ApiGateway.DTOs;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.TestInfrastructure.Helpers;

namespace QuantTrader.ApiGateway.Tests.Controllers;

public sealed class SettingsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly FakeRedisCacheService _fakeRedis = new();

    public SettingsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real Redis with our fake
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRedisCacheService));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddSingleton<IRedisCacheService>(_fakeRedis);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetProviders_ReturnsAllProviders()
    {
        var response = await _client.GetAsync("/api/settings/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var providers = await response.Content.ReadFromJsonAsync<List<ApiProviderInfoResponse>>();
        providers.Should().NotBeNull();
        providers!.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetExchangeSettings_WhenNoneConfigured_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/settings/exchanges");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<List<ExchangeSettingsResponse>>();
        settings.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task SaveExchangeSettings_ValidBinanceKey_Returns200()
    {
        var request = new SaveExchangeSettingsRequest(
            Exchange: "Binance",
            ApiKey: new string('A', 64),
            ApiSecret: new string('B', 64),
            UseTestnet: true);

        var response = await _client.PostAsJsonAsync("/api/settings/exchanges", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ExchangeSettingsResponse>();
        result.Should().NotBeNull();
        result!.Exchange.Should().Be("Binance");
    }

    [Fact]
    public async Task SaveExchangeSettings_MissingApiKey_ReturnsBadRequest()
    {
        var request = new SaveExchangeSettingsRequest(
            Exchange: "Binance",
            ApiKey: "",
            ApiSecret: new string('B', 64),
            UseTestnet: true);

        var response = await _client.PostAsJsonAsync("/api/settings/exchanges", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteExchangeSettings_WhenNotFound_Returns404()
    {
        var response = await _client.DeleteAsync("/api/settings/exchanges/Binance");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
