using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Collections;
using GameCollector.Domain.Collections;
using GameCollector.Domain.Users;
using GameCollector.Domain.Sync;
using System.Text.Json;

namespace GameCollector.Application.Collections;

public sealed class OwnershipService(
    ICurrentUser currentUser,
    IUserProfileRepository users,
    ICollectionRepository collections,
    ICollectionGameRepository collectionGames,
    IWishlistRepository wishlist,
    ICatalogRepository catalog,
    ISyncRepository sync,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IOwnershipService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<Result<IReadOnlyList<OwnedGameDto>>> GetCollectionGamesAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(collectionId, cancellationToken);
        if (access.Error is not null) return Result.Failure<IReadOnlyList<OwnedGameDto>>(access.Error);
        var items = await collectionGames.GetForCollectionAsync(collectionId, cancellationToken);
        return Result.Success<IReadOnlyList<OwnedGameDto>>(items.Select(item => new OwnedGameDto(item.GameId, item.Game.Title, item.Game.Publisher, item.Game.ModerationStatus.ToString(), item.AddedAtUtc)).ToList());
    }

    public async Task<Result<bool>> AddToCollectionAsync(Guid collectionId, Guid gameId, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(collectionId, cancellationToken);
        if (access.Error is not null) return Result.Failure<bool>(access.Error);
        if (!CanEdit(access.Collection!, access.Profile!.Id)) return Result.Failure<bool>(ApplicationErrors.CollectionEditRequired);
        var game = await catalog.GetVisibleByIdAsync(gameId, access.Profile.Id, currentUser.IsAdministrator, cancellationToken);
        if (game is null) return Result.Failure<bool>(ApplicationErrors.GameNotFound);
        var existing = await collectionGames.GetAsync(collectionId, gameId, cancellationToken);
        if (existing?.IsOwned is true) return Result.Success(true);
        var now = Now();
        var state = existing ?? CollectionGame.Create(Guid.NewGuid(), collectionId, gameId, access.Profile.Id, now);
        if (existing is null) await collectionGames.AddAsync(state, cancellationToken);
        else state.Apply(true, access.Profile.Id, now, state.LastServerSequence);
        var collectionEvent = SyncEvent.Create("collection", collectionId, "collectionGameChanged", gameId,
            JsonSerializer.Serialize(new { GameId = gameId, IsPresent = true }, JsonOptions), now);
        await sync.AddEventAsync(collectionEvent, cancellationToken);
        var wished = await wishlist.GetAsync(access.Profile.Id, gameId, cancellationToken);
        SyncEvent? wishlistEvent = null;
        if (wished?.IsPresent is true)
        {
            wished.Apply(false, now, wished.LastServerSequence);
            wishlistEvent = SyncEvent.Create("user", access.Profile.Id, "wishlistGameChanged", gameId,
                JsonSerializer.Serialize(new { GameId = gameId, IsPresent = false }, JsonOptions), now);
            await sync.AddEventAsync(wishlistEvent, cancellationToken);
        }
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.SaveChangesAsync(token);
                state.Apply(true, access.Profile.Id, now, collectionEvent.Sequence);
                if (wished is not null && wishlistEvent is not null) wished.Apply(false, now, wishlistEvent.Sequence);
                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);
        }
        catch (PersistenceConflictException exception) when (exception.Constraint == PersistenceConstraints.CollectionGame) { return Result.Success(true); }
        return Result.Success(true);
    }

    public async Task<Result<bool>> RemoveFromCollectionAsync(Guid collectionId, Guid gameId, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(collectionId, cancellationToken);
        if (access.Error is not null) return Result.Failure<bool>(access.Error);
        if (!CanEdit(access.Collection!, access.Profile!.Id)) return Result.Failure<bool>(ApplicationErrors.CollectionEditRequired);
        var item = await collectionGames.GetAsync(collectionId, gameId, cancellationToken);
        if (item?.IsOwned is true)
        {
            var now = Now(); item.Apply(false, access.Profile!.Id, now, item.LastServerSequence);
            var syncEvent = SyncEvent.Create("collection", collectionId, "collectionGameChanged", gameId,
                JsonSerializer.Serialize(new { GameId = gameId, IsPresent = false }, JsonOptions), now);
            await sync.AddEventAsync(syncEvent, cancellationToken);
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            { await unitOfWork.SaveChangesAsync(token); item.Apply(false, access.Profile.Id, now, syncEvent.Sequence); await unitOfWork.SaveChangesAsync(token); }, cancellationToken);
        }
        return Result.Success(true);
    }

    public async Task<Result<IReadOnlyList<WishlistGameDto>>> GetWishlistAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<WishlistGameDto>>(ApplicationErrors.ProfileNotFound);
        var items = await wishlist.GetForUserAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<WishlistGameDto>>(items.Select(item => new WishlistGameDto(item.GameId, item.Game.Title, item.Game.Publisher, item.Game.ModerationStatus.ToString(), item.CreatedAtUtc)).ToList());
    }

    public async Task<Result<bool>> AddToWishlistAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetVisibleByIdAsync(gameId, profile.Id, currentUser.IsAdministrator, cancellationToken);
        if (game is null) return Result.Failure<bool>(ApplicationErrors.GameNotFound);
        var existing = await wishlist.GetAsync(profile.Id, gameId, cancellationToken);
        if (existing?.IsPresent is true) return Result.Success(true);
        var now = Now(); var state = existing ?? WishlistItem.Create(Guid.NewGuid(), profile.Id, gameId, now);
        if (existing is null) await wishlist.AddAsync(state, cancellationToken); else state.Apply(true, now, state.LastServerSequence);
        var syncEvent = SyncEvent.Create("user", profile.Id, "wishlistGameChanged", gameId,
            JsonSerializer.Serialize(new { GameId = gameId, IsPresent = true }, JsonOptions), now);
        await sync.AddEventAsync(syncEvent, cancellationToken);
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            { await unitOfWork.SaveChangesAsync(token); state.Apply(true, now, syncEvent.Sequence); await unitOfWork.SaveChangesAsync(token); }, cancellationToken);
        }
        catch (PersistenceConflictException exception) when (exception.Constraint == PersistenceConstraints.WishlistItem) { return Result.Success(true); }
        return Result.Success(true);
    }

    public async Task<Result<bool>> RemoveFromWishlistAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var item = await wishlist.GetAsync(profile.Id, gameId, cancellationToken);
        if (item?.IsPresent is true)
        {
            var now = Now(); item.Apply(false, now, item.LastServerSequence);
            var syncEvent = SyncEvent.Create("user", profile.Id, "wishlistGameChanged", gameId,
                JsonSerializer.Serialize(new { GameId = gameId, IsPresent = false }, JsonOptions), now);
            await sync.AddEventAsync(syncEvent, cancellationToken);
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            { await unitOfWork.SaveChangesAsync(token); item.Apply(false, now, syncEvent.Sequence); await unitOfWork.SaveChangesAsync(token); }, cancellationToken);
        }
        return Result.Success(true);
    }

    private async Task<(UserProfile? Profile, Collection? Collection, ApplicationError? Error)> GetAccessAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return (null, null, ApplicationErrors.ProfileNotFound);
        var collection = await collections.GetByIdAsync(id, cancellationToken);
        if (collection is null) return (profile, null, ApplicationErrors.CollectionNotFound);
        return collection.CanView(profile.Id) ? (profile, collection, null) : (profile, collection, ApplicationErrors.CollectionAccessDenied);
    }
    private Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private static bool CanEdit(Collection collection, Guid userId) => collection.OwnerUserId == userId || collection.GetMemberRole(userId) == CollectionRole.Editor;
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
