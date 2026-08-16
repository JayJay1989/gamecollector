using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCollector.Api.Tests.Infrastructure;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Catalog;
using GameCollector.Contracts.Collections;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Catalog;
using GameCollector.Domain.Collections;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Api.Tests;

public sealed class OwnershipWorkflowTests(GameCollectorApiFactory factory) : IClassFixture<GameCollectorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OwnershipIsBinaryAndAcquisitionClearsActorsWishlist()
    {
        var owner = await CreateUserAsync();
        var collection = await CreateCollectionAsync(owner);
        var game = await FindUnoAsync(owner);

        using var wishlistAdd = Request(HttpMethod.Put, $"{ApiRoutes.V1}/me/wishlist/{game.Id}", owner);
        using var wishlistAddResponse = await _client.SendAsync(wishlistAdd);
        Assert.Equal(HttpStatusCode.NoContent, wishlistAddResponse.StatusCode);

        using var firstAdd = Request(HttpMethod.Put, $"{ApiRoutes.V1}/collections/{collection.Id}/games/{game.Id}", owner);
        using var firstAddResponse = await _client.SendAsync(firstAdd);
        Assert.Equal(HttpStatusCode.NoContent, firstAddResponse.StatusCode);
        using var duplicateAdd = Request(HttpMethod.Put, $"{ApiRoutes.V1}/collections/{collection.Id}/games/{game.Id}", owner);
        using var duplicateAddResponse = await _client.SendAsync(duplicateAdd);
        Assert.Equal(HttpStatusCode.NoContent, duplicateAddResponse.StatusCode);

        using var gamesRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/collections/{collection.Id}/games", owner);
        using var gamesResponse = await _client.SendAsync(gamesRequest);
        var owned = await gamesResponse.Content.ReadFromJsonAsync<List<OwnedGameDto>>();
        Assert.Single(owned!, item => item.GameId == game.Id);

        using var wishlistRequest = Request(HttpMethod.Get, ApiRoutes.V1 + "/me/wishlist", owner);
        using var wishlistResponse = await _client.SendAsync(wishlistRequest);
        var wishlist = await wishlistResponse.Content.ReadFromJsonAsync<List<WishlistGameDto>>();
        Assert.Empty(wishlist!);
    }

    [Fact]
    public async Task ViewerCanReadButOnlyEditorOrOwnerCanMutateOwnership()
    {
        var owner = await CreateUserAsync();
        var member = await CreateUserAsync();
        var collection = await CreateCollectionAsync(owner);
        await AddMemberDirectlyAsync(collection.Id, member.UserId, CollectionRole.Viewer);
        var game = await FindUnoAsync(owner);

        using var viewerAdd = Request(HttpMethod.Put, $"{ApiRoutes.V1}/collections/{collection.Id}/games/{game.Id}", member);
        using var viewerAddResponse = await _client.SendAsync(viewerAdd);
        Assert.Equal(HttpStatusCode.Forbidden, viewerAddResponse.StatusCode);
        Assert.Equal(CollectionErrorCodes.CollectionEditRequired, await ErrorCodeAsync(viewerAddResponse));

        using var roleRequest = Request(HttpMethod.Patch, $"{ApiRoutes.V1}/collections/{collection.Id}/members/{member.UserId}", owner);
        roleRequest.Content = JsonContent.Create(new UpdateCollectionMemberRequest(CollectionMemberRoleDto.Editor));
        using var roleResponse = await _client.SendAsync(roleRequest);
        Assert.Equal(HttpStatusCode.NoContent, roleResponse.StatusCode);

        using var editorAdd = Request(HttpMethod.Put, $"{ApiRoutes.V1}/collections/{collection.Id}/games/{game.Id}", member);
        using var editorAddResponse = await _client.SendAsync(editorAdd);
        Assert.Equal(HttpStatusCode.NoContent, editorAddResponse.StatusCode);
        using var editorRemove = Request(HttpMethod.Delete, $"{ApiRoutes.V1}/collections/{collection.Id}/games/{game.Id}", member);
        using var editorRemoveResponse = await _client.SendAsync(editorRemove);
        Assert.Equal(HttpStatusCode.NoContent, editorRemoveResponse.StatusCode);
    }

    [Fact]
    public async Task PendingOwnedGameBecomesVisibleToCollectionMembersOnly()
    {
        var submitter = await CreateUserAsync();
        var member = await CreateUserAsync();
        var stranger = await CreateUserAsync();
        var collection = await CreateCollectionAsync(submitter);
        await AddMemberDirectlyAsync(collection.Id, member.UserId, CollectionRole.Viewer);
        var pendingId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pending = Game.Create(pendingId, "Shared Pending Game", null, null, null, null, null,
                null, null, null, ModerationStatus.Pending, submitter.UserId, DateTime.UtcNow);
            await dbContext.Games.AddAsync(pending);
            _ = await dbContext.SaveChangesAsync();
        }

        using var beforeOwnership = Request(HttpMethod.Get, $"{ApiRoutes.V1}/games/{pendingId}", member);
        using var beforeResponse = await _client.SendAsync(beforeOwnership);
        Assert.Equal(HttpStatusCode.NotFound, beforeResponse.StatusCode);

        using var addRequest = Request(HttpMethod.Put, $"{ApiRoutes.V1}/collections/{collection.Id}/games/{pendingId}", submitter);
        using var addResponse = await _client.SendAsync(addRequest);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        using var memberRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/games/{pendingId}", member);
        using var memberResponse = await _client.SendAsync(memberRequest);
        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
        using var strangerRequest = Request(HttpMethod.Get, $"{ApiRoutes.V1}/games/{pendingId}", stranger);
        using var strangerResponse = await _client.SendAsync(strangerRequest);
        Assert.Equal(HttpStatusCode.NotFound, strangerResponse.StatusCode);
    }

    [Fact]
    public async Task WishlistsArePersonal()
    {
        var first = await CreateUserAsync();
        var second = await CreateUserAsync();
        var game = await FindUnoAsync(first);
        using var add = Request(HttpMethod.Put, $"{ApiRoutes.V1}/me/wishlist/{game.Id}", first);
        using var addResponse = await _client.SendAsync(add);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);
        using var secondList = Request(HttpMethod.Get, ApiRoutes.V1 + "/me/wishlist", second);
        using var secondResponse = await _client.SendAsync(secondList);
        var items = await secondResponse.Content.ReadFromJsonAsync<List<WishlistGameDto>>();
        Assert.Empty(items!);
    }

    private async Task<TestUser> CreateUserAsync()
    {
        var subject = "subject-" + Guid.NewGuid().ToString("N"); var deviceId = Guid.NewGuid();
        using var onboard = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.V1 + "/me/onboarding");
        onboard.Headers.Add(TestAuthenticationHandler.UserHeader, subject);
        onboard.Content = JsonContent.Create(new OnboardUserRequest("Ownership User", "u" + Guid.NewGuid().ToString("N")[..12]));
        using var onboardResponse = await _client.SendAsync(onboard);
        var profile = await onboardResponse.Content.ReadFromJsonAsync<UserProfileDto>(); Assert.NotNull(profile);
        var user = new TestUser(subject, deviceId, profile.Id);
        using var activate = Request(HttpMethod.Post, ApiRoutes.V1 + "/me/device/activate", user);
        activate.Content = JsonContent.Create(new ActivateDeviceRequest(deviceId, "fcm-" + Guid.NewGuid().ToString("N")));
        using var activation = await _client.SendAsync(activate); Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        return user;
    }

    private async Task<CollectionDto> CreateCollectionAsync(TestUser user)
    {
        using var request = Request(HttpMethod.Post, ApiRoutes.V1 + "/collections", user);
        request.Content = JsonContent.Create(new CreateCollectionRequest("Owned Games " + Guid.NewGuid().ToString("N")[..6]));
        using var response = await _client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<CollectionDto>(); Assert.NotNull(result); return result;
    }

    private async Task<GameSummaryDto> FindUnoAsync(TestUser user)
    {
        using var request = Request(HttpMethod.Get, ApiRoutes.V1 + "/games/search?q=UNO", user);
        using var response = await _client.SendAsync(request);
        var games = await response.Content.ReadFromJsonAsync<List<GameSummaryDto>>(); return Assert.Single(games!, item => item.Title == "UNO Flip!");
    }

    private async Task AddMemberDirectlyAsync(Guid collectionId, Guid userId, CollectionRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var collection = await dbContext.Collections.Include(item => item.Members).SingleAsync(item => item.Id == collectionId);
        collection.AddMember(Guid.NewGuid(), userId, role, DateTime.UtcNow); _ = await dbContext.SaveChangesAsync();
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, TestUser user)
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Add(TestAuthenticationHandler.UserHeader, user.Subject);
        request.Headers.Add(DeviceHeaders.DeviceId, user.DeviceId.ToString()); return request;
    }
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()); return json.RootElement.GetProperty("code").GetString();
    }
    private sealed record TestUser(string Subject, Guid DeviceId, Guid UserId);
}
