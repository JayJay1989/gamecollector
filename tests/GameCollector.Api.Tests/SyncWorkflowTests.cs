using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using GameCollector.Contracts.Collections;
using GameCollector.Contracts.Sync;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Sync;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class SyncWorkflowTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RetriedAndOutOfOrderMutationsAreIdempotentAndLastAcceptedWins()
    {
        var user = await CreateUserAsync();
        var collection = await CreateCollectionAsync(user);
        var gameId = await GetSeedGameIdAsync(user);
        var addId = Guid.NewGuid();

        var added = await PushAsync(user, new SyncMutationDto(addId, SyncMutationTypes.AddCollectionGame, gameId, collection.Id));
        var duplicate = await PushAsync(user, new SyncMutationDto(addId, SyncMutationTypes.AddCollectionGame, gameId, collection.Id));
        Assert.True(added.Applied);
        Assert.False(added.Duplicate);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(added.ServerSequence, duplicate.ServerSequence);

        var removed = await PushAsync(user, new SyncMutationDto(Guid.NewGuid(), SyncMutationTypes.RemoveCollectionGame, gameId, collection.Id));
        var readded = await PushAsync(user, new SyncMutationDto(Guid.NewGuid(), SyncMutationTypes.AddCollectionGame, gameId, collection.Id));
        Assert.True(readded.ServerSequence > removed.ServerSequence);

        using var pullRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/sync/pull", user);
        pullRequest.Content = JsonContent.Create(new SyncPullRequest(
            [new SyncScopeDto("collection", collection.Id, removed.ServerSequence!.Value)], 100));
        using var pullResponse = await _client.SendAsync(pullRequest);
        var pull = await pullResponse.Content.ReadFromJsonAsync<SyncPullResponse>();
        var change = Assert.Single(Assert.Single(pull!.Scopes).Changes);
        Assert.Equal(readded.ServerSequence, change.Sequence);
        Assert.True(change.Payload.GetProperty("isPresent").GetBoolean());

        using var ownedRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/collections/{collection.Id}/games", user);
        using var ownedResponse = await _client.SendAsync(ownedRequest);
        var owned = await ownedResponse.Content.ReadFromJsonAsync<List<OwnedGameDto>>();
        Assert.Contains(owned!, item => item.GameId == gameId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var state = await dbContext.CollectionGames.SingleAsync(item => item.CollectionId == collection.Id && item.GameId == gameId);
        Assert.True(state.IsOwned);
        Assert.Equal(readded.ServerSequence, state.LastServerSequence);
        Assert.Equal(3, await dbContext.ProcessedMutations.CountAsync(item => item.UserId == user.UserId));
    }

    [Fact]
    public async Task RemovalLeavesTombstoneAndBootstrapReturnsAllAccessibleScopes()
    {
        var user = await CreateUserAsync();
        var collection = await CreateCollectionAsync(user);
        var gameId = await GetSeedGameIdAsync(user);
        _ = await PushAsync(user, new SyncMutationDto(Guid.NewGuid(), SyncMutationTypes.AddCollectionGame, gameId, collection.Id));
        var removed = await PushAsync(user, new SyncMutationDto(Guid.NewGuid(), SyncMutationTypes.RemoveCollectionGame, gameId, collection.Id));

        using var bootstrapRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/sync/bootstrap", user);
        using var bootstrapResponse = await _client.SendAsync(bootstrapRequest);
        Assert.Equal(HttpStatusCode.OK, bootstrapResponse.StatusCode);
        var bootstrap = await bootstrapResponse.Content.ReadFromJsonAsync<SyncBootstrapDto>();
        Assert.Contains(bootstrap!.Snapshot, item => item.ScopeType == "catalog" && item.Operation == "snapshot");
        Assert.Contains(bootstrap.Snapshot, item => item.ScopeType == "user" && item.ScopeId == user.UserId);
        var collectionSnapshot = Assert.Single(bootstrap.Snapshot, item => item.ScopeType == "collection" && item.ScopeId == collection.Id);
        var games = collectionSnapshot.Payload.GetProperty("games");
        Assert.Equal(JsonValueKind.Array, games.ValueKind);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tombstone = await dbContext.CollectionGames.SingleAsync(item => item.CollectionId == collection.Id && item.GameId == gameId);
        Assert.False(tombstone.IsOwned);
        Assert.Equal(removed.ServerSequence, tombstone.LastServerSequence);
    }

    [Fact]
    public async Task RetentionFloorRequiresScopeResetAndForeignCollectionIsForbidden()
    {
        var owner = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var collection = await CreateCollectionAsync(owner);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.SyncRetentionStates.AddAsync(new SyncRetentionState($"collection:{collection.Id:N}", 50));
            _ = await dbContext.SaveChangesAsync();
        }

        using var staleRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/sync/pull", owner);
        staleRequest.Content = JsonContent.Create(new SyncPullRequest([new SyncScopeDto("collection", collection.Id, 1)]));
        using var staleResponse = await _client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal(SyncErrorCodes.SyncResetRequired, await ErrorCodeAsync(staleResponse));

        using var forbiddenRequest = Request(HttpMethod.Post, ApiRoutes.V1 + "/sync/pull", outsider);
        forbiddenRequest.Content = JsonContent.Create(new SyncPullRequest([new SyncScopeDto("collection", collection.Id, 1)]));
        using var forbiddenResponse = await _client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(SyncErrorCodes.SyncScopeAccessDenied, await ErrorCodeAsync(forbiddenResponse));
    }

    private async Task<SyncMutationResultDto> PushAsync(TestUser user, SyncMutationDto mutation)
    {
        using var request = Request(HttpMethod.Post, ApiRoutes.V1 + "/sync/push", user);
        request.Content = JsonContent.Create(new SyncPushRequest([mutation]));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncPushResponse>();
        return Assert.Single(result!.Results);
    }

    private async Task<CollectionDto> CreateCollectionAsync(TestUser user)
    {
        using var request = Request(HttpMethod.Post, ApiRoutes.V1 + "/collections", user);
        request.Content = JsonContent.Create(new CreateCollectionRequest("Synced Collection"));
        using var response = await _client.SendAsync(request);
        var collection = await response.Content.ReadFromJsonAsync<CollectionDto>();
        return Assert.IsType<CollectionDto>(collection);
    }

    private async Task<Guid> GetSeedGameIdAsync(TestUser user)
    {
        using var request = Request(HttpMethod.Get, ApiRoutes.V1 + "/games/search?q=flip", user);
        using var response = await _client.SendAsync(request);
        var games = await response.Content.ReadFromJsonAsync<List<GameSummaryDto>>();
        return Assert.Single(games!).Id;
    }

    private async Task<TestUser> CreateUserAsync()
    {
        var subject = "sync-" + Guid.NewGuid().ToString("N"); var deviceId = Guid.NewGuid();
        using var onboard = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding");
        onboard.Headers.Add(TestAuthenticationHandler.UserHeader, subject);
        onboard.Content = JsonContent.Create(new OnboardUserRequest("Sync User", "u" + Guid.NewGuid().ToString("N")[..12]));
        using var onboardResponse = await _client.SendAsync(onboard);
        var profile = await onboardResponse.Content.ReadFromJsonAsync<UserProfileDto>(); Assert.NotNull(profile);
        var user = new TestUser(subject, deviceId, profile.Id);
        using var activate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", user);
        activate.Content = JsonContent.Create(new ActivateDeviceRequest(deviceId, "fcm-" + Guid.NewGuid().ToString("N")));
        using var activateResponse = await _client.SendAsync(activate); Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return user;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, TestUser user)
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        request.Headers.Add(DeviceHeaders.DeviceId, user.DeviceId.ToString()); return request;
    }
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("code").GetString();
    }
    private sealed record TestUser(string Subject, Guid DeviceId, Guid UserId);
}
