using System.Net;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;

namespace GameCollector.Api.Tests;

public sealed class HardeningTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ResponsesContainDefensiveSecurityHeaders()
    {
        using var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("default-src 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        Assert.Equal("no-store", Assert.Single(response.Headers.GetValues("Cache-Control")));
    }

    [Fact]
    public async Task OversizedRequestReturnsStableProblemDetails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/test/security/validation")
        {
            Content = new ByteArrayContent(new byte[1_048_577])
        };
        request.Content.Headers.ContentType = new("application/json");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(ApiErrorCodes.RequestTooLarge, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task TimedOutRequestReturnsStableProblemDetails()
    {
        using var response = await _client.GetAsync(ApiRoutes.V1 + "/test/security/slow");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(ApiErrorCodes.RequestTimedOut, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task RateLimitIsPartitionedByAuthenticatedSubject()
    {
        var subject = "rate-limit-" + Guid.NewGuid().ToString("N");
        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 121; attempt++)
        {
            last?.Dispose();
            using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.V1 + "/test/security/user");
            request.Headers.Add(TestAuthenticationHandler.UserHeader, subject);
            last = await _client.SendAsync(request);
        }
        using (last)
        {
            Assert.NotNull(last);
            Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
            Assert.Equal(ApiErrorCodes.RateLimitExceeded, await ErrorCodeAsync(last));
            Assert.True(last.Headers.Contains("Retry-After"));
        }
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("code").GetString();
    }
}
