using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;

namespace GameCollector.Api.Tests;

public sealed class AuthenticationTests(GameCollectorApiFactory factory)
    : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UserEndpointWithoutTokenReturnsProblemDetailsUnauthorized()
    {
        using var response = await _client.GetAsync(ApiRoutes.V1 + "/test/security/user");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(ApiErrorCodes.NotAuthenticated, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthenticatedUserCanUseUserEndpoint()
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, ApiRoutes.V1 + "/test/security/user");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NormalUserCannotUseAdministratorEndpoint()
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, ApiRoutes.V1 + "/test/security/admin");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(ApiErrorCodes.NotAllowed, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AdministratorCanUseAdministratorEndpoint()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            ApiRoutes.V1 + "/test/security/admin",
            "gamecollector-admin");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string? roles = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, "test-user-id");
        if (roles is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.RolesHeader, roles);
        }

        return request;
    }
}
