using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Middleware;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;

namespace GameCollector.Api.Tests;

public sealed class ApiConventionsTests(GameCollectorApiFactory factory)
    : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenApiV1DocumentIsAvailable()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("3.1.1", json.RootElement.GetProperty("openapi").GetString());
    }

    [Fact]
    public async Task SuppliedCorrelationIdIsReturnedAndIncludedInProblemDetails()
    {
        const string correlationId = "mobile-request-123";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/missing");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(correlationId, json.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal(ApiErrorCodes.EntityMissing, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidModelReturnsStableValidationError()
    {
        using var response = await _client.PostAsJsonAsync(
            ApiRoutes.V1 + "/test/security/validation",
            new { name = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(ApiErrorCodes.InvalidRequest, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnexpectedExceptionDoesNotExposeInternalDetail()
    {
        using var response = await _client.GetAsync(ApiRoutes.V1 + "/test/security/exception");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Sensitive test detail", body, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(ApiErrorCodes.UnexpectedError, json.RootElement.GetProperty("code").GetString());
    }
}
