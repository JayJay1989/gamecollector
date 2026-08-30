using System.Net;
using System.Net.Http.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Collections;
using GameCollector.Contracts.Notifications;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Catalog;
using GameCollector.Domain.Collections;
using GameCollector.Domain.Users;
using GameCollector.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class FriendWorkflowTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AcceptedFriendCanSeeOnlyPublicCollectionsAndWishlist()
    {
        var first = await CreateUserAsync("first user");
        var second = await CreateUserAsync("second user");
        var stranger = await CreateUserAsync("stranger");
        var (publicCollectionId, privateCollectionId, gameId) = await SeedFriendContentAsync(second.UserId);

        using var send = Request(HttpMethod.Post, ApiRoutes.V1 + "/friends/requests", first);
        send.Content = JsonContent.Create(new CreateFriendRequest(second.UserId));
        using var sendResponse = await _client.SendAsync(send);
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        var friendRequest = (await sendResponse.Content.ReadFromJsonAsync<FriendRequestDto>())!;

        using var denied = Request(HttpMethod.Get, $"{ApiRoutes.V1}/friends/{second.UserId}", first);
        using var deniedResponse = await _client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);

        using var accept = Request(HttpMethod.Post, $"{ApiRoutes.V1}/friends/requests/{friendRequest.Id}/accept", second);
        using var acceptResponse = await _client.SendAsync(accept);
        Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);

        using var profileRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/friends/{second.UserId}", first);
        using var profileResponse = await _client.SendAsync(profileRequest);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profile = (await profileResponse.Content.ReadFromJsonAsync<FriendProfileDto>())!;
        Assert.Contains(profile.PublicCollections, item => item.Id == publicCollectionId && item.GameCount == 1);
        Assert.DoesNotContain(profile.PublicCollections, item => item.Id == privateCollectionId);
        Assert.Contains(profile.Wishlist, item => item.GameId == gameId);

        using var gamesRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/friends/{second.UserId}/collections/{publicCollectionId}/games", first);
        using var gamesResponse = await _client.SendAsync(gamesRequest);
        Assert.Equal(HttpStatusCode.OK, gamesResponse.StatusCode);
        Assert.Contains((await gamesResponse.Content.ReadFromJsonAsync<List<OwnedGameDto>>())!, item => item.GameId == gameId);

        using var strangerRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/friends/{second.UserId}", stranger);
        using var strangerResponse = await _client.SendAsync(strangerRequest);
        Assert.Equal(HttpStatusCode.Forbidden, strangerResponse.StatusCode);
    }

    [Fact]
    public async Task NotificationCanBeDeletedPermanently()
    {
        var first = await CreateUserAsync("notification sender");
        var second = await CreateUserAsync("notification owner");
        using var send = Request(HttpMethod.Post, ApiRoutes.V1 + "/friends/requests", first);
        send.Content = JsonContent.Create(new CreateFriendRequest(second.UserId));
        using var sendResponse = await _client.SendAsync(send);
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);

        using var list = Request(HttpMethod.Get, ApiRoutes.V1 + "/me/notifications", second);
        using var listResponse = await _client.SendAsync(list);
        var notification = (await listResponse.Content.ReadFromJsonAsync<List<NotificationDto>>())!
            .Single(item => item.Type == NotificationTypes.FriendRequest);
        using var delete = Request(HttpMethod.Delete, $"{ApiRoutes.V1}/me/notifications/{notification.Id}", second);
        using var deleteResponse = await _client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        using var refreshed = Request(HttpMethod.Get, ApiRoutes.V1 + "/me/notifications", second);
        using var refreshedResponse = await _client.SendAsync(refreshed);
        Assert.DoesNotContain((await refreshedResponse.Content.ReadFromJsonAsync<List<NotificationDto>>())!, item => item.Id == notification.Id);
    }

    private async Task<(Guid PublicId, Guid PrivateId, Guid GameId)> SeedFriendContentAsync(Guid ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var publicCollection = Collection.Create(Guid.NewGuid(), "public games", ownerId, now);
        publicCollection.Update(publicCollection.Name, true, now);
        var privateCollection = Collection.Create(Guid.NewGuid(), "private games", ownerId, now);
        var game = Game.Create(Guid.NewGuid(), "friend game", null, null, 2026, null, null, null, null, null,
            ModerationStatus.Approved, null, now);
        await db.Collections.AddRangeAsync(publicCollection, privateCollection);
        await db.Games.AddAsync(game);
        await db.CollectionGames.AddAsync(CollectionGame.Create(Guid.NewGuid(), publicCollection.Id, game.Id, ownerId, now));
        await db.WishlistItems.AddAsync(WishlistItem.Create(Guid.NewGuid(), ownerId, game.Id, now));
        await db.SaveChangesAsync();
        return (publicCollection.Id, privateCollection.Id, game.Id);
    }

    private async Task<TestUser> CreateUserAsync(string displayName)
    {
        var user = new TestUser("friend-" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.Empty);
        using var onboard = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding");
        onboard.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        onboard.Content = JsonContent.Create(new OnboardUserRequest(displayName, "u" + Guid.NewGuid().ToString("N")[..12]));
        using var onboardResponse = await _client.SendAsync(onboard);
        var profile = (await onboardResponse.Content.ReadFromJsonAsync<UserProfileDto>())!;
        user = user with { UserId = profile.Id };
        using var activate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", user);
        activate.Content = JsonContent.Create(new ActivateDeviceRequest(user.DeviceId, "fcm-" + Guid.NewGuid().ToString("N")));
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

    private sealed record TestUser(string Subject, Guid DeviceId, Guid UserId);
}
