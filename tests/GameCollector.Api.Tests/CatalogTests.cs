using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using GameCollector.Contracts.Media;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Catalog;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class CatalogTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ApprovedSeedGameCanBeFoundByTitleAndBarcode()
    {
        var user = await CreateUserAsync();
        using var searchRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/games/search?q=flip", user);
        using var searchResponse = await _client.SendAsync(searchRequest);
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var results = await searchResponse.Content.ReadFromJsonAsync<List<GameSummaryDto>>();
        Assert.Contains(results!, game => game.Title == "UNO Flip!");

        using var barcodeRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/games/barcode/887961751062", user);
        using var barcodeResponse = await _client.SendAsync(barcodeRequest);
        Assert.Equal(HttpStatusCode.OK, barcodeResponse.StatusCode);
        var game = await barcodeResponse.Content.ReadFromJsonAsync<GameDto>();
        Assert.Equal("UNO Flip!", game?.Title);
        Assert.Contains("887961751062", game!.Barcodes);
        Assert.Contains(game.Languages, language => language.Name == "English");
        Assert.Contains(game.Tags, tag => tag.Name == "Card Game");
    }

    [Fact]
    public async Task ProductLookupReturnsLocalCatalogBeforeExternalProvider()
    {
        var user = await CreateUserAsync();
        using var request = Request(HttpMethod.Get, ApiRoutes.V1 + "/product-lookup/887961751062", user);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidate = await response.Content.ReadFromJsonAsync<ProductMetadataCandidateDto>();
        Assert.Equal("catalog", candidate?.Source);
        Assert.NotNull(candidate?.ExistingGameId);
        Assert.Equal("UNO Flip!", candidate?.Title);
    }

    [Fact]
    public async Task PendingGameIsVisibleOnlyToSubmitter()
    {
        var submitter = await CreateUserAsync();
        var otherUser = await CreateUserAsync();
        var pendingId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var submitterProfile = await dbContext.UserProfiles.SingleAsync(user => user.IdentitySubject == submitter.Subject);
            var pending = Game.Create(pendingId, "Private Pending Game", null, "Test Publisher", 2026,
                2, 4, 8, 20, 30, ModerationStatus.Pending, submitterProfile.Id, DateTime.UtcNow);
            pending.AddBarcode(Guid.NewGuid(), CreateUniqueEan13());
            await dbContext.Games.AddAsync(pending);
            _ = await dbContext.SaveChangesAsync();
        }

        using var submitterRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/games/{pendingId}", submitter);
        using var submitterResponse = await _client.SendAsync(submitterRequest);
        Assert.Equal(HttpStatusCode.OK, submitterResponse.StatusCode);

        using var otherRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/games/{pendingId}", otherUser);
        using var otherResponse = await _client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        Assert.Equal(CatalogErrorCodes.GameNotFound, await ErrorCodeAsync(otherResponse));
    }

    [Fact]
    public async Task ReferenceDataIsCanonicalAndInvalidBarcodeHasStableError()
    {
        var user = await CreateUserAsync();
        using var languagesRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/languages", user);
        using var languagesResponse = await _client.SendAsync(languagesRequest);
        var languages = await languagesResponse.Content.ReadFromJsonAsync<List<ReferenceDataDto>>();
        Assert.Contains(languages!, language => language.Code == "nl" && language.Name == "Dutch");

        using var tagsRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/tags", user);
        using var tagsResponse = await _client.SendAsync(tagsRequest);
        var tags = await tagsResponse.Content.ReadFromJsonAsync<List<ReferenceDataDto>>();
        Assert.Contains(tags!, tag => tag.Name == "Cooperative");

        using var invalidRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/games/barcode/123", user);
        using var invalidResponse = await _client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidResponse.StatusCode);
        Assert.Equal(CatalogErrorCodes.InvalidBarcode, await ErrorCodeAsync(invalidResponse));
    }

    [Fact]
    public async Task DuplicateNormalizedBarcodeIsRejectedBySqlite()
    {
        _ = await CreateUserAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var duplicate = Game.Create(Guid.NewGuid(), "Duplicate Barcode Game", null, null, null,
            null, null, null, null, null, ModerationStatus.Approved, null, DateTime.UtcNow);
        duplicate.AddBarcode(Guid.NewGuid(), "887961751062");
        await dbContext.Games.AddAsync(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private async Task<TestUser> CreateUserAsync()
    {
        var subject = "subject-" + Guid.NewGuid().ToString("N");
        var username = "u" + Guid.NewGuid().ToString("N")[..12];
        var deviceId = Guid.NewGuid();
        using var onboard = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding");
        onboard.Headers.Add(TestAuthenticationHandler.UserHeader, subject);
        onboard.Content = JsonContent.Create(new OnboardUserRequest("Catalog User", username));
        using var onboardResponse = await _client.SendAsync(onboard);
        var profile = await onboardResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        var user = new TestUser(subject, deviceId);
        using var activate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", user);
        activate.Content = JsonContent.Create(new ActivateDeviceRequest(deviceId, "fcm-" + Guid.NewGuid().ToString("N")));
        using var activateResponse = await _client.SendAsync(activate);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return user;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, TestUser user)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        request.Headers.Add(DeviceHeaders.DeviceId, user.DeviceId.ToString());
        return request;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private static string CreateUniqueEan13()
    {
        var digits = "9" + Random.Shared.NextInt64(100_000_000_000).ToString("D11", System.Globalization.CultureInfo.InvariantCulture);
        var sum = 0;
        for (var index = 0; index < digits.Length; index++)
        {
            sum += (digits[index] - '0') * (index % 2 == 0 ? 1 : 3);
        }

        return digits + ((10 - (sum % 10)) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record TestUser(string Subject, Guid DeviceId);
}
