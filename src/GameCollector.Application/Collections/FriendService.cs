using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Application.Notifications;
using GameCollector.Contracts.Collections;
using GameCollector.Contracts.Notifications;
using GameCollector.Domain.Common;
using GameCollector.Domain.Users;

namespace GameCollector.Application.Collections;

public sealed class FriendService(
    ICurrentUser currentUser, IUserProfileRepository users, IFriendshipRepository friendships,
    ICollectionRepository collections, ICollectionGameRepository collectionGames, IWishlistRepository wishlist,
    IGameImageRepository images, INotificationWriter notifications, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IFriendService
{
    public async Task<Result<IReadOnlyList<FriendDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var profile = await CurrentAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<FriendDto>>(ApplicationErrors.ProfileNotFound);
        var items = await friendships.GetForUserAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<FriendDto>>(items.Select(item => MapFriend(item, profile.Id)).ToList());
    }

    public async Task<Result<IReadOnlyList<FriendRequestDto>>> ListRequestsAsync(CancellationToken cancellationToken = default)
    {
        var profile = await CurrentAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<FriendRequestDto>>(ApplicationErrors.ProfileNotFound);
        var items = await friendships.GetPendingForUserAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<FriendRequestDto>>(items.Select(item => MapRequest(item, profile.Id)).ToList());
    }

    public async Task<Result<FriendRequestDto>> SendRequestAsync(CreateFriendRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await CurrentAsync(cancellationToken);
        if (profile is null) return Result.Failure<FriendRequestDto>(ApplicationErrors.ProfileNotFound);
        var target = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (target is null || target.IsDisabled || target.Id == profile.Id)
            return Result.Failure<FriendRequestDto>(ApplicationErrors.Validation("Select another active user."));
        var existing = await friendships.GetBetweenAsync(profile.Id, target.Id, cancellationToken);
        if (existing?.Status == FriendshipStatus.Accepted)
            return Result.Failure<FriendRequestDto>(ApplicationErrors.Validation("You are already friends."));
        if (existing?.Status == FriendshipStatus.Pending)
            return Result.Success(MapRequest(existing, profile.Id));
        if (existing is not null)
        {
            friendships.Remove(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        var item = Friendship.Create(Guid.NewGuid(), profile.Id, target.Id, Now());
        await friendships.AddAsync(item, cancellationToken);
        await notifications.CreateAsync(target.Id, NotificationTypes.FriendRequest,
            new { FriendRequestId = item.Id, UserId = profile.Id, profile.DisplayName, profile.Username }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(MapRequest(item, profile.Id));
    }

    public async Task<Result<bool>> RespondAsync(Guid id, bool accept, CancellationToken cancellationToken = default)
    {
        var profile = await CurrentAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var item = await friendships.GetByIdAsync(id, cancellationToken);
        if (item is null || item.AddresseeUserId != profile.Id || item.Status != FriendshipStatus.Pending)
            return Result.Failure<bool>(ApplicationErrors.Validation("The friend request is no longer available."));
        try
        {
            if (accept) item.Accept(Now()); else item.Decline(Now());
            await notifications.CreateAsync(item.RequesterUserId,
                accept ? NotificationTypes.FriendRequestAccepted : NotificationTypes.FriendRequestDeclined,
                new { FriendRequestId = item.Id, UserId = profile.Id, profile.DisplayName, profile.Username }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(true);
        }
        catch (DomainValidationException exception) { return Result.Failure<bool>(ApplicationErrors.Validation(exception.Message)); }
    }

    public async Task<Result<bool>> RemoveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await CurrentAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var item = await friendships.GetBetweenAsync(profile.Id, userId, cancellationToken);
        if (item is null || item.Status != FriendshipStatus.Accepted) return Result.Success(true);
        friendships.Remove(item); await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(true);
    }

    public async Task<Result<FriendProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await CurrentAsync(cancellationToken);
        if (profile is null) return Result.Failure<FriendProfileDto>(ApplicationErrors.ProfileNotFound);
        if (!await friendships.AreFriendsAsync(profile.Id, userId, cancellationToken))
            return Result.Failure<FriendProfileDto>(ApplicationErrors.CollectionAccessDenied);
        var target = await users.GetByIdAsync(userId, cancellationToken);
        if (target is null) return Result.Failure<FriendProfileDto>(ApplicationErrors.ProfileNotFound);
        var publicCollections = await collections.GetPublicForOwnerAsync(userId, cancellationToken);
        var collectionDtos = new List<FriendCollectionDto>();
        foreach (var item in publicCollections)
            collectionDtos.Add(new FriendCollectionDto(item.Id, item.Name, (await collectionGames.GetForCollectionAsync(item.Id, cancellationToken)).Count));
        var wished = await wishlist.GetForUserAsync(userId, cancellationToken);
        return Result.Success(new FriendProfileDto(target.Id, target.DisplayName, target.Username, collectionDtos,
            wished.Select(item => new WishlistGameDto(item.GameId, item.Game.Title, item.Game.Publisher,
                item.Game.ModerationStatus.ToString(), item.CreatedAtUtc)).ToList()));
    }

    public async Task<Result<IReadOnlyList<OwnedGameDto>>> GetCollectionGamesAsync(Guid userId, Guid collectionId, CancellationToken cancellationToken = default)
    {
        var profile = await CurrentAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<OwnedGameDto>>(ApplicationErrors.ProfileNotFound);
        if (!await friendships.AreFriendsAsync(profile.Id, userId, cancellationToken))
            return Result.Failure<IReadOnlyList<OwnedGameDto>>(ApplicationErrors.CollectionAccessDenied);
        var collection = await collections.GetByIdAsync(collectionId, cancellationToken);
        if (collection is null || collection.OwnerUserId != userId || !collection.IsPublic)
            return Result.Failure<IReadOnlyList<OwnedGameDto>>(ApplicationErrors.CollectionNotFound);
        var games = await collectionGames.GetForCollectionAsync(collectionId, cancellationToken);
        var fronts = await images.GetReadyFrontIdsAsync(games.Select(item => item.GameId).ToList(), cancellationToken);
        return Result.Success<IReadOnlyList<OwnedGameDto>>(games.Select(item => new OwnedGameDto(item.GameId,
            item.Game.Title, item.Game.Publisher, item.Game.ReleaseYear, item.Game.ModerationStatus.ToString(),
            fronts.GetValueOrDefault(item.GameId), item.AddedAtUtc)).ToList());
    }

    private Task<UserProfile?> CurrentAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(
        currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private static FriendDto MapFriend(Friendship item, Guid currentId)
    {
        var other = item.RequesterUserId == currentId ? item.Addressee : item.Requester;
        return new FriendDto(other.Id, other.DisplayName, other.Username, item.UpdatedAtUtc);
    }
    private static FriendRequestDto MapRequest(Friendship item, Guid currentId)
    {
        var incoming = item.AddresseeUserId == currentId;
        var other = incoming ? item.Requester : item.Addressee;
        return new FriendRequestDto(item.Id, other.Id, other.DisplayName, other.Username, incoming,
            item.Status.ToString(), item.CreatedAtUtc);
    }
}
