using System.Net;
using FluentAssertions;
using Xunit;

namespace QuantTrader.ApiGateway.Tests.Controllers;

[Collection("ApiGateway")]
public sealed class MarketDataControllerTests
{
    private readonly HttpClient _client;

    public MarketDataControllerTests(ApiGatewayFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetPrices_EndpointExists()
    {
        var response = await _client.GetAsync("/api/market/prices");
        // Should not return 404 (endpoint exists); may return 200 or other status
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }
}
