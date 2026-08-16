using System.Net;
using System.Net.Http.Json;
using GameCollector.Api.Authentication;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Admin;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using GameCollector.Contracts.Collections;
using GameCollector.Contracts.Users;
using GameCollector.Contracts.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class AdministrationWorkflowTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AdministratorCanManageUsersCatalogAuditAndDiagnostics()
    {
        var admin = await CreateUserAsync("Admin Operator", activateDevice: false);
        var user = await CreateUserAsync("Managed User", activateDevice: true);
        var collection = await CreateCollectionAsync(user);

        using (var bootstrap = Request(HttpMethod.Get, ApiRoutes.V1 + "/sync/bootstrap", user))
        using (var bootstrapResponse = await _client.SendAsync(bootstrap))
            Assert.Equal(HttpStatusCode.OK, bootstrapResponse.StatusCode);
        using (var invalidSync = Request(HttpMethod.Post, ApiRoutes.V1 + "/sync/pull", user))
        {
            invalidSync.Content = JsonContent.Create(new SyncPullRequest([]));
            using var invalidSyncResponse = await _client.SendAsync(invalidSync);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidSyncResponse.StatusCode);
        }

        using (var forbidden = Request(HttpMethod.Get, ApiRoutes.AdminV1 + "/users", user))
        using (var forbiddenResponse = await _client.SendAsync(forbidden))
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var users = await GetAsync<List<AdminUserSummaryDto>>(ApiRoutes.AdminV1 + "/users?q=Managed", admin, true);
        Assert.Contains(users, item => item.Id == user.UserId);
        var detail = await GetAsync<AdminUserDetailDto>($"{ApiRoutes.AdminV1}/users/{user.UserId}", admin, true);
        Assert.Equal(user.DeviceId, detail.ActiveDevice?.DeviceId);
        Assert.Contains(detail.Collections, item => item.Id == collection.Id && item.IsOwner);

        var collections = await GetAsync<List<AdminCollectionSummaryDto>>(ApiRoutes.AdminV1 + "/collections?q=Managed", admin, true);
        Assert.Contains(collections, item => item.Id == collection.Id);
        var collectionDetail = await GetAsync<AdminCollectionDetailDto>($"{ApiRoutes.AdminV1}/collections/{collection.Id}", admin, true);
        Assert.Contains(collectionDetail.Members, item => item.UserId == user.UserId && item.Role == "Owner");

        var createGame = new AdminGameRequest("Administrator Game", "Created directly", "Publisher", 2026,
            2, 4, 8, 20, 40, [], [], []);
        using var createRequest = Request(HttpMethod.Post, ApiRoutes.AdminV1 + "/games", admin, administrator: true);
        createRequest.Headers.Add("X-Correlation-ID", "admin-create-game");
        createRequest.Content = JsonContent.Create(createGame);
        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var game = await createResponse.Content.ReadFromJsonAsync<GameDto>();
        Assert.NotNull(game);
        Assert.Equal("Approved", game.ModerationStatus);

        var stale = createGame with { Title = "Stale", ExpectedRevision = 99 };
        using var staleRequest = Request(HttpMethod.Put, $"{ApiRoutes.AdminV1}/games/{game.Id}", admin, true);
        staleRequest.Content = JsonContent.Create(stale);
        using var staleResponse = await _client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        var update = createGame with { Title = "Updated Administrator Game", ExpectedRevision = game.Revision };
        using var updateRequest = Request(HttpMethod.Put, $"{ApiRoutes.AdminV1}/games/{game.Id}", admin, true);
        updateRequest.Content = JsonContent.Create(update);
        using var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(2, (await updateResponse.Content.ReadFromJsonAsync<GameDto>())?.Revision);

        using (var selfDisable = Request(HttpMethod.Post, $"{ApiRoutes.AdminV1}/users/{admin.UserId}/disable", admin, true))
        using (var selfDisableResponse = await _client.SendAsync(selfDisable))
            Assert.Equal(HttpStatusCode.Conflict, selfDisableResponse.StatusCode);

        await PostAdminAsync($"{ApiRoutes.AdminV1}/users/{user.UserId}/disable", admin);
        using (var blocked = Request(HttpMethod.Get, ApiRoutes.V1 + "/me", user))
        using (var blockedResponse = await _client.SendAsync(blocked))
            Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);
        await PostAdminAsync($"{ApiRoutes.AdminV1}/users/{user.UserId}/enable", admin);
        using (var enabled = Request(HttpMethod.Get, ApiRoutes.V1 + "/me", user))
        using (var enabledResponse = await _client.SendAsync(enabled))
            Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);

        await PostAdminAsync($"{ApiRoutes.AdminV1}/users/{user.UserId}/revoke-device", admin);
        using (var revoked = Request(HttpMethod.Patch, ApiRoutes.V1 + "/me", user))
        {
            revoked.Content = JsonContent.Create(new UpdateUserProfileRequest("Revoked Device", null));
        using (var revokedResponse = await _client.SendAsync(revoked))
            Assert.Equal(HttpStatusCode.Forbidden, revokedResponse.StatusCode);
        }

        var audit = await GetAsync<List<AdminAuditDto>>(
            $"{ApiRoutes.AdminV1}/audit?entityType=UserProfile&entityId={user.UserId}", admin, true);
        Assert.Contains(audit, item => item.Action == "UserDisabled");
        Assert.Contains(audit, item => item.Action == "UserEnabled");
        Assert.Contains(audit, item => item.Action == "DeviceRevoked");
        Assert.DoesNotContain(audit, item => (item.Before?.ToString() + item.After?.ToString()).Contains("fcm-", StringComparison.OrdinalIgnoreCase));

        var sync = await GetAsync<List<SyncDiagnosticDto>>(
            $"{ApiRoutes.AdminV1}/diagnostics/sync?userId={user.UserId}", admin, true);
        var diagnostic = Assert.Single(sync);
        Assert.Equal(user.DeviceId, diagnostic.DeviceId);
        Assert.True(diagnostic.DownloadedEvents > 0);
        Assert.NotNull(diagnostic.LastSuccessfulSyncAtUtc);
        Assert.Equal(SyncErrorCodes.InvalidSyncRequest, diagnostic.LastError);
        Assert.NotNull(diagnostic.LastErrorAtUtc);

        await PostAdminAsync($"{ApiRoutes.AdminV1}/users/{admin.UserId}/disable", user);
        using var disabledAdminRequest = Request(HttpMethod.Get, ApiRoutes.AdminV1 + "/users", admin, true);
        using var disabledAdminResponse = await _client.SendAsync(disabledAdminRequest);
        Assert.Equal(HttpStatusCode.Forbidden, disabledAdminResponse.StatusCode);
    }

    [Fact]
    public void EveryAdminRouteRequiresAdministratorPolicy()
    {
        var sources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var endpoints = sources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(ApiRoutes.AdminV1.TrimStart('/'), StringComparison.OrdinalIgnoreCase) is true)
            .ToList();
        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint => Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => authorization.Policy == AuthorizationPolicies.Administrator));
    }

    private async Task<TestUser> CreateUserAsync(string displayName, bool activateDevice)
    {
        var user = new TestUser("admin-workflow-" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.Empty);
        using var onboard = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding", user);
        onboard.Content = JsonContent.Create(new OnboardUserRequest(displayName, "u" + Guid.NewGuid().ToString("N")[..12]));
        using var onboardResponse = await _client.SendAsync(onboard);
        Assert.Equal(HttpStatusCode.Created, onboardResponse.StatusCode);
        user = user with { UserId = (await onboardResponse.Content.ReadFromJsonAsync<UserProfileDto>())!.Id };
        if (activateDevice)
        {
            using var activate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", user);
            activate.Content = JsonContent.Create(new ActivateDeviceRequest(user.DeviceId, "fcm-" + Guid.NewGuid().ToString("N")));
            using var response = await _client.SendAsync(activate);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        return user;
    }

    private async Task<CollectionDto> CreateCollectionAsync(TestUser user)
    {
        using var request = Request(HttpMethod.Post, ApiRoutes.V1 + "/collections", user);
        request.Content = JsonContent.Create(new CreateCollectionRequest("Managed Collection"));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CollectionDto>())!;
    }

    private async Task<T> GetAsync<T>(string path, TestUser user, bool administrator)
    {
        using var request = Request(HttpMethod.Get, path, user, administrator);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task PostAdminAsync(string path, TestUser admin)
    {
        using var request = Request(HttpMethod.Post, path, admin, administrator: true);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, TestUser user, bool administrator = false)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        if (user.DeviceId != Guid.Empty) request.Headers.Add(DeviceHeaders.DeviceId, user.DeviceId.ToString());
        if (administrator) request.Headers.Add(TestAuthenticationHandler.RolesHeader, "gamecollector-admin");
        return request;
    }

    private sealed record TestUser(string Subject, Guid DeviceId, Guid UserId);
}
