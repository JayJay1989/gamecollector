using System.Net;
using GameCollector.Api.Tests.Infrastructure;

namespace GameCollector.Api.Tests;

public sealed class HealthEndpointsTests : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(GameCollectorApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpointReturnsOk(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
