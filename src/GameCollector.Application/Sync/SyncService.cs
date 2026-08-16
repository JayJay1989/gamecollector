using System.Text.Json;
using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Api;
using GameCollector.Contracts.Sync;
using GameCollector.Domain.Collections;
using GameCollector.Domain.Sync;
using GameCollector.Domain.Users;
using GameCollector.Application.Abstractions.Auditing;

namespace GameCollector.Application.Sync;

public sealed class SyncService(
    ICurrentUser currentUser, IUserProfileRepository users, ICollectionRepository collections,
    ICollectionGameRepository collectionGames, IWishlistRepository wishlist, ICatalogRepository catalog,
    ICollectionInvitationRepository invitations, INotificationRepository notifications,
    ISyncRepository sync, ISyncDiagnosticRepository diagnostics, IAuditContext auditContext, IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ISyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<SyncPushResponse>> PushAsync(SyncPushRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<SyncPushResponse>(ApplicationErrors.ProfileNotFound);
        if (request.Mutations.Count is < 1 or > 100 || request.Mutations.Any(item => item.MutationId == Guid.Empty || item.GameId == Guid.Empty))
        {
            await RecordFailureAsync(profile.Id, SyncErrorCodes.InvalidSyncRequest, cancellationToken);
            return Result.Failure<SyncPushResponse>(ApplicationErrors.InvalidSyncRequest);
        }
        var results = new List<SyncMutationResultDto>(request.Mutations.Count);
        foreach (var mutation in request.Mutations)
            results.Add(await ProcessMutationAsync(profile, mutation, cancellationToken));
        await RecordSuccessAsync(profile.Id, await sync.GetMaximumSequenceAsync(cancellationToken), request.Mutations.Count, 0,
            results.FirstOrDefault(item => !item.Applied)?.ErrorCode, cancellationToken);
        return Result.Success(new SyncPushResponse(results));
    }

    public async Task<Result<SyncPullResponse>> PullAsync(SyncPullRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<SyncPullResponse>(ApplicationErrors.ProfileNotFound);
        if (request.Scopes.Count is < 1 or > 20 || request.Limit is < 1 or > 500 || request.Scopes.Any(item => item.Cursor < 0))
        {
            await RecordFailureAsync(profile.Id, SyncErrorCodes.InvalidSyncRequest, cancellationToken);
            return Result.Failure<SyncPullResponse>(ApplicationErrors.InvalidSyncRequest);
        }
        var pages = new List<SyncScopePageDto>(request.Scopes.Count);
        foreach (var requested in request.Scopes)
        {
            var scope = await ValidateScopeAsync(profile, requested, cancellationToken);
            if (scope.Error is not null)
            {
                await RecordFailureAsync(profile.Id, scope.Error.Code, cancellationToken);
                return Result.Failure<SyncPullResponse>(scope.Error);
            }
            if (requested.Cursor == 0)
            {
                pages.Add(await CreateSnapshotPageAsync(profile, scope.Type!, scope.Id, cancellationToken));
                continue;
            }
            var floor = await sync.GetMinimumCursorAsync(scope.Type!, scope.Id, cancellationToken);
            if (requested.Cursor < floor)
            {
                await RecordFailureAsync(profile.Id, SyncErrorCodes.SyncResetRequired, cancellationToken);
                return Result.Failure<SyncPullResponse>(ApplicationErrors.SyncResetRequired);
            }
            var events = await sync.GetEventsAsync(scope.Type!, scope.Id, requested.Cursor, request.Limit + 1, cancellationToken);
            var hasMore = events.Count > request.Limit;
            var selected = events.Take(request.Limit).Select(MapEvent).ToList();
            var next = selected.Count == 0 ? requested.Cursor : selected[^1].Sequence;
            pages.Add(new SyncScopePageDto(scope.Type!, scope.Id, next, hasMore, false, selected));
        }
        await RecordSuccessAsync(profile.Id, pages.Count == 0 ? 0 : pages.Max(page => page.NextCursor), 0,
            pages.Sum(page => page.Changes.Count), null, cancellationToken);
        return Result.Success(new SyncPullResponse(pages));
    }

    public async Task<Result<SyncBootstrapDto>> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<SyncBootstrapDto>(ApplicationErrors.ProfileNotFound);
        var cursor = await sync.GetMaximumSequenceAsync(cancellationToken);
        var snapshot = new List<SyncChangeDto>();
        snapshot.AddRange((await CreateSnapshotPageAsync(profile, "catalog", null, cancellationToken, cursor)).Changes);
        snapshot.AddRange((await CreateSnapshotPageAsync(profile, "user", profile.Id, cancellationToken, cursor)).Changes);
        var accessible = await collections.GetForUserAsync(profile.Id, cancellationToken);
        foreach (var collection in accessible)
            snapshot.AddRange((await CreateSnapshotPageAsync(profile, "collection", collection.Id, cancellationToken, cursor)).Changes);
        await RecordSuccessAsync(profile.Id, cursor, 0, snapshot.Count, null, cancellationToken);
        return Result.Success(new SyncBootstrapDto(cursor, snapshot));
    }

    private async Task<SyncMutationResultDto> ProcessMutationAsync(UserProfile profile, SyncMutationDto mutation, CancellationToken cancellationToken)
    {
        var prior = await sync.GetProcessedMutationAsync(profile.Id, mutation.MutationId, cancellationToken);
        if (prior is not null) return Duplicate(prior);
        SyncMutationResultDto? result = null;
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
            {
                var duplicate = await sync.GetProcessedMutationAsync(profile.Id, mutation.MutationId, transactionToken);
                if (duplicate is not null) { result = Duplicate(duplicate); return; }
                var validation = await ValidateMutationAsync(profile, mutation, transactionToken);
                if (validation is not null)
                {
                    result = new SyncMutationResultDto(mutation.MutationId, false, false, null, validation);
                    await StoreResultAsync(profile.Id, result, transactionToken); await unitOfWork.SaveChangesAsync(transactionToken); return;
                }

                var now = Now();
                var scopeType = mutation.CollectionId.HasValue ? "collection" : "user";
                var scopeId = mutation.CollectionId ?? profile.Id;
                var isPresent = mutation.Type is SyncMutationTypes.AddCollectionGame or SyncMutationTypes.AddWishlistGame;
                var operation = mutation.CollectionId.HasValue ? "collectionGameChanged" : "wishlistGameChanged";
                var payload = JsonSerializer.Serialize(new { mutation.GameId, IsPresent = isPresent }, JsonOptions);
                var syncEvent = SyncEvent.Create(scopeType, scopeId, operation, mutation.GameId, payload, now);
                await sync.AddEventAsync(syncEvent, transactionToken);

                CollectionGame? collectionState = null;
                WishlistItem? wishlistState = null;
                if (mutation.CollectionId.HasValue)
                {
                    collectionState = await collectionGames.GetAsync(mutation.CollectionId.Value, mutation.GameId, transactionToken);
                    if (collectionState is null)
                    {
                        collectionState = CollectionGame.Create(Guid.NewGuid(), mutation.CollectionId.Value, mutation.GameId, profile.Id, now);
                        if (!isPresent) collectionState.Apply(false, profile.Id, now, 0);
                        await collectionGames.AddAsync(collectionState, transactionToken);
                    }
                    else collectionState.Apply(isPresent, profile.Id, now, collectionState.LastServerSequence);
                }
                else
                {
                    wishlistState = await wishlist.GetAsync(profile.Id, mutation.GameId, transactionToken);
                    if (wishlistState is null)
                    {
                        wishlistState = WishlistItem.Create(Guid.NewGuid(), profile.Id, mutation.GameId, now);
                        if (!isPresent) wishlistState.Apply(false, now, 0);
                        await wishlist.AddAsync(wishlistState, transactionToken);
                    }
                    else wishlistState.Apply(isPresent, now, wishlistState.LastServerSequence);
                }

                await unitOfWork.SaveChangesAsync(transactionToken);
                if (collectionState is not null) collectionState.Apply(isPresent, profile.Id, now, syncEvent.Sequence);
                if (wishlistState is not null) wishlistState.Apply(isPresent, now, syncEvent.Sequence);
                result = new SyncMutationResultDto(mutation.MutationId, true, false, syncEvent.Sequence, null);
                await StoreResultAsync(profile.Id, result, transactionToken);
                await unitOfWork.SaveChangesAsync(transactionToken);
            }, cancellationToken);
        }
        catch (PersistenceConflictException)
        {
            var duplicate = await sync.GetProcessedMutationAsync(profile.Id, mutation.MutationId, cancellationToken);
            if (duplicate is not null) return Duplicate(duplicate);
            throw;
        }
        return result ?? throw new InvalidOperationException("The mutation did not produce a result.");
    }

    private async Task<string?> ValidateMutationAsync(UserProfile profile, SyncMutationDto mutation, CancellationToken cancellationToken)
    {
        var supported = mutation.Type is SyncMutationTypes.AddCollectionGame or SyncMutationTypes.RemoveCollectionGame
            or SyncMutationTypes.AddWishlistGame or SyncMutationTypes.RemoveWishlistGame;
        if (!supported) return SyncErrorCodes.UnsupportedMutation;
        var collectionMutation = mutation.Type is SyncMutationTypes.AddCollectionGame or SyncMutationTypes.RemoveCollectionGame;
        if (collectionMutation != mutation.CollectionId.HasValue) return SyncErrorCodes.InvalidSyncRequest;
        if (mutation.CollectionId.HasValue)
        {
            var collection = await collections.GetByIdAsync(mutation.CollectionId.Value, cancellationToken);
            if (collection is null || !collection.CanView(profile.Id)) return CollectionErrorCodes.CollectionNotFound;
            if (collection.OwnerUserId != profile.Id && collection.GetMemberRole(profile.Id) != CollectionRole.Editor)
                return CollectionErrorCodes.CollectionEditRequired;
        }
        var game = await catalog.GetVisibleByIdAsync(mutation.GameId, profile.Id, currentUser.IsAdministrator, cancellationToken);
        return game is null ? CatalogErrorCodes.GameNotFound : null;
    }

    private async Task<(string? Type, Guid? Id, ApplicationError? Error)> ValidateScopeAsync(UserProfile profile, SyncScopeDto scope, CancellationToken cancellationToken)
    {
        var type = scope.Type.Trim().ToLowerInvariant();
        if (type == "catalog" && scope.Id is null) return (type, null, null);
        if (type == "user" && scope.Id == profile.Id) return (type, profile.Id, null);
        if (type == "collection" && scope.Id.HasValue)
        {
            var collection = await collections.GetByIdAsync(scope.Id.Value, cancellationToken);
            return collection?.CanView(profile.Id) is true ? (type, scope.Id, null) : (null, null, ApplicationErrors.SyncScopeAccessDenied);
        }
        return (null, null, ApplicationErrors.InvalidSyncScope);
    }

    private async Task<SyncScopePageDto> CreateSnapshotPageAsync(UserProfile profile, string type, Guid? id,
        CancellationToken cancellationToken, long? fixedCursor = null)
    {
        var cursor = fixedCursor ?? await sync.GetMaximumSequenceAsync(cancellationToken);
        object payload;
        Guid entityId;
        if (type == "catalog")
        {
            var games = await catalog.GetAllVisibleAsync(profile.Id, currentUser.IsAdministrator, cancellationToken);
            var languages = await catalog.GetLanguagesAsync(cancellationToken); var tags = await catalog.GetTagsAsync(cancellationToken);
            payload = new
            {
                Games = games.Select(game => new { game.Id, game.Title, game.Description, game.Publisher, game.ReleaseYear, game.MinimumPlayers, game.MaximumPlayers, game.MinimumAge, game.MinimumPlayingTimeMinutes, game.MaximumPlayingTimeMinutes, ModerationStatus = game.ModerationStatus.ToString(), game.Revision, Barcodes = game.Barcodes.Select(item => item.NormalizedBarcode), LanguageIds = game.Languages.Select(item => item.LanguageId), TagIds = game.Tags.Select(item => item.TagId) }),
                Languages = languages.Select(item => new { item.Id, item.Code, item.Name }), Tags = tags.Select(item => new { item.Id, item.Name })
            };
            entityId = profile.Id;
        }
        else if (type == "user")
        {
            var accessible = await collections.GetForUserAsync(profile.Id, cancellationToken);
            var wished = await wishlist.GetStatesForUserAsync(profile.Id, cancellationToken);
            var pendingInvitations = await invitations.GetForInviteeAsync(profile.Id, cancellationToken);
            var recentNotifications = await notifications.GetForUserAsync(profile.Id, 100, cancellationToken);
            payload = new
            {
                Profile = new { profile.Id, profile.DisplayName, profile.Username, profile.DefaultCollectionId, profile.UpdatedAtUtc },
                Collections = accessible.Select(item => new { item.Id, item.Name, item.OwnerUserId, item.UpdatedAtUtc }),
                Wishlist = wished.Select(item => new { item.GameId, item.IsPresent, item.LastServerSequence, item.ChangedAtUtc }),
                Invitations = pendingInvitations.Select(item => new { item.Id, item.CollectionId, item.InviterUserId, item.Role, Status = item.Status.ToString(), item.CreatedAtUtc }),
                Notifications = recentNotifications.Select(item => new { item.Id, item.Type, Payload = JsonSerializer.Deserialize<JsonElement>(item.PayloadJson, JsonOptions), item.CreatedAtUtc, item.ReadAtUtc })
            };
            entityId = profile.Id;
        }
        else
        {
            var collection = await collections.GetByIdAsync(id!.Value, cancellationToken)
                ?? throw new InvalidOperationException("A validated collection disappeared.");
            var states = await collectionGames.GetStatesForCollectionsAsync([collection.Id], cancellationToken);
            payload = new
            {
                Collection = new { collection.Id, collection.Name, collection.OwnerUserId, collection.UpdatedAtUtc },
                Members = collection.Members.Select(item => new { item.UserId, Role = item.Role.ToString(), item.JoinedAtUtc }),
                Games = states.Select(item => new { item.GameId, item.IsOwned, item.LastServerSequence, item.ChangedAtUtc })
            };
            entityId = collection.Id;
        }
        var change = new SyncChangeDto(cursor, type, id, "snapshot", entityId,
            JsonSerializer.SerializeToElement(payload, JsonOptions), Now());
        return new SyncScopePageDto(type, id, cursor, false, true, [change]);
    }

    private async Task StoreResultAsync(Guid userId, SyncMutationResultDto result, CancellationToken cancellationToken) =>
        await sync.AddProcessedMutationAsync(ProcessedMutation.Create(result.MutationId, userId, Now(),
            JsonSerializer.Serialize(result, JsonOptions)), cancellationToken);
    private static SyncMutationResultDto Duplicate(ProcessedMutation mutation)
    {
        var result = JsonSerializer.Deserialize<SyncMutationResultDto>(mutation.ResultJson, JsonOptions)
            ?? throw new InvalidDataException("A processed mutation result is invalid.");
        return result with { Duplicate = true };
    }
    private static SyncChangeDto MapEvent(SyncEvent item) => new(item.Sequence, item.ScopeType, item.ScopeId,
        item.Operation, item.EntityId, JsonSerializer.Deserialize<JsonElement>(item.PayloadJson, JsonOptions), item.OccurredAtUtc);
    private Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(
        currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private async Task RecordSuccessAsync(Guid userId, long cursor, int uploaded, int downloaded,
        string? error, CancellationToken cancellationToken)
    {
        if (!auditContext.DeviceId.HasValue) return;
        var item = await diagnostics.GetAsync(userId, auditContext.DeviceId.Value, cancellationToken);
        if (item is null)
        {
            item = SyncDiagnostic.Create(Guid.NewGuid(), userId, auditContext.DeviceId.Value);
            await diagnostics.AddAsync(item, cancellationToken);
        }
        item.RecordSuccess(cursor, uploaded, downloaded, Now());
        if (!string.IsNullOrWhiteSpace(error)) item.RecordFailure(error, Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(Guid userId, string error, CancellationToken cancellationToken)
    {
        if (!auditContext.DeviceId.HasValue) return;
        var item = await diagnostics.GetAsync(userId, auditContext.DeviceId.Value, cancellationToken);
        if (item is null)
        {
            item = SyncDiagnostic.Create(Guid.NewGuid(), userId, auditContext.DeviceId.Value);
            await diagnostics.AddAsync(item, cancellationToken);
        }
        item.RecordFailure(error, Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
