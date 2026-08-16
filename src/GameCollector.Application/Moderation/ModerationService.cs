using System.Text.Json;
using GameCollector.Application.Abstractions.Auditing;
using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Media;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Catalog;
using GameCollector.Domain.Auditing;
using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;
using GameCollector.Domain.Users;
using GameCollector.Domain.Sync;
using GameCollector.Application.Notifications;
using GameCollector.Contracts.Notifications;

namespace GameCollector.Application.Moderation;

public sealed class ModerationService(
    ICurrentUser currentUser, IAuditContext auditContext, IUserProfileRepository users,
    ICatalogRepository catalog, IGameImageRepository images, IGameChangeRequestRepository changeRequests,
    IAuditLogRepository auditLogs, ISyncRepository sync, INotificationWriter notificationWriter,
    IObjectStorage storage, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IModerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<GameSubmissionDto>> CreateSubmissionAsync(UpsertGameSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<GameSubmissionDto>(ApplicationErrors.ProfileNotFound);
        var references = await ValidateReferencesAsync(request, cancellationToken);
        if (references is not null) return Result.Failure<GameSubmissionDto>(references);
        try
        {
            var now = Now();
            var game = Game.Create(Guid.NewGuid(), request.Title, request.Description, request.Publisher,
                request.ReleaseYear, request.MinimumPlayers, request.MaximumPlayers, request.MinimumAge,
                request.MinimumPlayingTimeMinutes, request.MaximumPlayingTimeMinutes,
                ModerationStatus.Draft, profile.Id, now);
            foreach (var barcode in request.Barcodes.Distinct(StringComparer.Ordinal)) game.AddBarcode(Guid.NewGuid(), barcode);
            foreach (var id in request.LanguageIds.Distinct()) game.AddLanguage(id);
            foreach (var id in request.TagIds.Distinct()) game.AddTag(id);
            await catalog.AddAsync(game, cancellationToken);
            await AddSyncEventAsync("user", profile.Id, "submissionChanged", game.Id,
                new { game.Id, game.Revision, ModerationStatus = game.ModerationStatus.ToString() }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(MapSubmission(game));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameSubmissionDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConflictException exception) when (exception.Constraint == PersistenceConstraints.GameBarcode)
        { return Result.Failure<GameSubmissionDto>(ApplicationErrors.Validation("A barcode is already assigned to another game.")); }
    }

    public async Task<Result<IReadOnlyList<GameSubmissionDto>>> GetMySubmissionsAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<GameSubmissionDto>>(ApplicationErrors.ProfileNotFound);
        var games = await catalog.GetSubmissionsForUserAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<GameSubmissionDto>>(games.Select(MapSubmission).ToList());
    }

    public async Task<Result<GameSubmissionDto>> GetMySubmissionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnedSubmissionAsync(id, cancellationToken);
        return access.Error is null ? Result.Success(MapSubmission(access.Game!)) : Result.Failure<GameSubmissionDto>(access.Error);
    }

    public async Task<Result<GameSubmissionDto>> UpdateSubmissionAsync(Guid id, UpsertGameSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnedSubmissionAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<GameSubmissionDto>(access.Error);
        var game = access.Game!;
        if (game.ModerationStatus is not (ModerationStatus.Draft or ModerationStatus.NeedsChanges)) return Result.Failure<GameSubmissionDto>(ApplicationErrors.SubmissionNotEditable);
        if (!request.ExpectedRevision.HasValue || request.ExpectedRevision.Value != game.Revision) return Result.Failure<GameSubmissionDto>(ApplicationErrors.RevisionConflict);
        var references = await ValidateReferencesAsync(request, cancellationToken);
        if (references is not null) return Result.Failure<GameSubmissionDto>(references);
        try
        {
            game.UpdateSubmission(request.Title, request.Description, request.Publisher, request.ReleaseYear,
                request.MinimumPlayers, request.MaximumPlayers, request.MinimumAge, request.MinimumPlayingTimeMinutes,
                request.MaximumPlayingTimeMinutes, request.Barcodes, request.LanguageIds, request.TagIds, Now());
            await AddSyncEventAsync("user", game.SubmittedByUserId!.Value, "submissionChanged", game.Id,
                new { game.Id, game.Revision, ModerationStatus = game.ModerationStatus.ToString() }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(MapSubmission(game));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameSubmissionDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConcurrencyException) { return Result.Failure<GameSubmissionDto>(ApplicationErrors.RevisionConflict); }
        catch (PersistenceConflictException exception) when (exception.Constraint == PersistenceConstraints.GameBarcode)
        { return Result.Failure<GameSubmissionDto>(ApplicationErrors.Validation("A barcode is already assigned to another game.")); }
    }

    public async Task<Result<bool>> DeleteSubmissionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnedSubmissionAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<bool>(access.Error);
        var game = access.Game!;
        if (game.ModerationStatus is not (ModerationStatus.Draft or ModerationStatus.NeedsChanges))
            return Result.Failure<bool>(ApplicationErrors.SubmissionNotEditable);
        var media = await images.GetForGameAsync(id, cancellationToken);
        catalog.Remove(game);
        await AddSyncEventAsync("user", game.SubmittedByUserId!.Value, "submissionDeleted", game.Id,
            new { game.Id }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var image in media)
        {
            foreach (var objectKey in new[] { image.OriginalObjectKey, image.ThumbnailObjectKey }.Where(key => !string.IsNullOrWhiteSpace(key)))
            {
                try { await storage.DeleteAsync(objectKey!, cancellationToken); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The database deletion is authoritative; orphaned object cleanup can be retried operationally.
                }
            }
        }
        return Result.Success(true);
    }

    public async Task<Result<GameSubmissionDto>> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var access = await GetOwnedSubmissionAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<GameSubmissionDto>(access.Error);
        if (access.Game!.ModerationStatus is not (ModerationStatus.Draft or ModerationStatus.NeedsChanges)) return Result.Failure<GameSubmissionDto>(ApplicationErrors.SubmissionNotEditable);
        if (!await images.HasReadyFrontAndBackAsync(id, cancellationToken)) return Result.Failure<GameSubmissionDto>(ApplicationErrors.RequiredImagesMissing);
        try { access.Game.Submit(Now()); await AddSyncEventAsync("user", access.Game.SubmittedByUserId!.Value, "submissionChanged", access.Game.Id, new { access.Game.Id, access.Game.Revision, ModerationStatus = access.Game.ModerationStatus.ToString() }, cancellationToken); await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(MapSubmission(access.Game)); }
        catch (PersistenceConcurrencyException) { return Result.Failure<GameSubmissionDto>(ApplicationErrors.RevisionConflict); }
    }

    public async Task<Result<GameChangeRequestDto>> CreateChangeRequestAsync(Guid gameId, CreateGameChangeRequestRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<GameChangeRequestDto>(ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetVisibleByIdAsync(gameId, profile.Id, currentUser.IsAdministrator, cancellationToken);
        if (game is null || game.ModerationStatus != ModerationStatus.Approved) return Result.Failure<GameChangeRequestDto>(ApplicationErrors.GameNotFound);
        if (IsEmpty(request.ProposedChanges)) return Result.Failure<GameChangeRequestDto>(ApplicationErrors.EmptyChangeRequest);
        if (await changeRequests.HasPendingAsync(gameId, profile.Id, cancellationToken)) return Result.Failure<GameChangeRequestDto>(ApplicationErrors.ChangeRequestAlreadyPending);
        try
        {
            ValidatePatch(request.ProposedChanges);
            var item = GameChangeRequest.Create(Guid.NewGuid(), gameId, profile.Id,
                JsonSerializer.Serialize(request.ProposedChanges, JsonOptions), Now());
            await changeRequests.AddAsync(item, cancellationToken);
            await AddSyncEventAsync("user", profile.Id, "changeRequestChanged", item.Id,
                new { item.Id, item.GameId, Status = item.Status.ToString() }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(MapChangeRequest(item));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameChangeRequestDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConflictException exception) when (exception.Constraint == PersistenceConstraints.PendingGameChangeRequest)
        { return Result.Failure<GameChangeRequestDto>(ApplicationErrors.ChangeRequestAlreadyPending); }
    }

    public async Task<Result<IReadOnlyList<GameChangeRequestDto>>> GetMyChangeRequestsAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<GameChangeRequestDto>>(ApplicationErrors.ProfileNotFound);
        var items = await changeRequests.GetForUserAsync(profile.Id, cancellationToken);
        return Result.Success<IReadOnlyList<GameChangeRequestDto>>(items.Select(MapChangeRequest).ToList());
    }

    public async Task<Result<IReadOnlyList<GameSubmissionDto>>> GetModerationQueueAsync(string? status, CancellationToken cancellationToken = default)
    {
        var parsed = ParseModerationStatus(status);
        if (parsed.Error is not null) return Result.Failure<IReadOnlyList<GameSubmissionDto>>(parsed.Error);
        var games = await catalog.GetSubmissionsForModerationAsync(parsed.Status, cancellationToken);
        return Result.Success<IReadOnlyList<GameSubmissionDto>>(games.Select(MapSubmission).ToList());
    }

    public async Task<Result<GameSubmissionDto>> GetSubmissionForModerationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var game = await catalog.GetByIdAsync(id, cancellationToken);
        return game?.SubmittedByUserId is not null && game.ModerationStatus != ModerationStatus.Draft
            ? Result.Success(MapSubmission(game)) : Result.Failure<GameSubmissionDto>(ApplicationErrors.SubmissionNotFound);
    }

    public Task<Result<GameSubmissionDto>> ApproveSubmissionAsync(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken = default) =>
        ModerateSubmissionAsync(id, request, "GameApproved", (game, admin, now) => game.Approve(admin, now), cancellationToken);
    public Task<Result<GameSubmissionDto>> RequestSubmissionChangesAsync(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken = default) =>
        ModerateSubmissionAsync(id, request, "GameChangesRequested", (game, admin, now) => game.RequestChanges(admin, request.Comment ?? string.Empty, now), cancellationToken);
    public Task<Result<GameSubmissionDto>> RejectSubmissionAsync(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken = default) =>
        ModerateSubmissionAsync(id, request, "GameRejected", (game, admin, now) => game.Reject(admin, request.Comment ?? string.Empty, now), cancellationToken);

    public async Task<Result<IReadOnlyList<GameChangeRequestDto>>> GetChangeRequestQueueAsync(string? status, CancellationToken cancellationToken = default)
    {
        GameChangeRequestStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<GameChangeRequestStatus>(status, true, out var value))
                return Result.Failure<IReadOnlyList<GameChangeRequestDto>>(ApplicationErrors.Validation("The change-request status is invalid."));
            parsed = value;
        }
        var items = await changeRequests.GetForModerationAsync(parsed, cancellationToken);
        return Result.Success<IReadOnlyList<GameChangeRequestDto>>(items.Select(MapChangeRequest).ToList());
    }

    public async Task<Result<GameChangeRequestDto>> ApproveChangeRequestAsync(Guid id, ReviewGameChangeRequestRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetChangeRequestForReviewAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<GameChangeRequestDto>(access.Error);
        if (access.Item!.Status != GameChangeRequestStatus.Pending) return Result.Failure<GameChangeRequestDto>(ApplicationErrors.ChangeRequestNotPending);
        if (access.Item.Game.Revision != request.ExpectedGameRevision) return Result.Failure<GameChangeRequestDto>(ApplicationErrors.RevisionConflict);
        try
        {
            var before = AuditGame(access.Item.Game);
            var patch = DeserializePatch(access.Item);
            access.Item.Game.ApplyApprovedCorrection(patch.Title, patch.Description, patch.Publisher, patch.ReleaseYear,
                patch.MinimumPlayers, patch.MaximumPlayers, patch.MinimumAge, patch.MinimumPlayingTimeMinutes,
                patch.MaximumPlayingTimeMinutes, Now());
            access.Item.Approve(access.Administrator!.Id, request.Comment, Now());
            await AddAuditAsync(access.Administrator.Id, "GameChangeRequestApproved", "GameChangeRequest", id,
                before, AuditGame(access.Item.Game), cancellationToken);
            await AddSyncEventAsync("catalog", null, "gameChanged", access.Item.Game.Id,
                new { access.Item.Game.Id, access.Item.Game.Revision, ModerationStatus = access.Item.Game.ModerationStatus.ToString() }, cancellationToken);
            await AddSyncEventAsync("user", access.Item.ProposedByUserId, "changeRequestChanged", access.Item.Id,
                new { access.Item.Id, Status = access.Item.Status.ToString() }, cancellationToken);
            await notificationWriter.CreateAsync(access.Item.ProposedByUserId, NotificationTypes.SuggestedEditApproved,
                new { ChangeRequestId = access.Item.Id, access.Item.GameId, access.Item.AdminComment }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(MapChangeRequest(access.Item));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameChangeRequestDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConcurrencyException) { return Result.Failure<GameChangeRequestDto>(ApplicationErrors.RevisionConflict); }
    }

    public async Task<Result<GameChangeRequestDto>> RejectChangeRequestAsync(Guid id, ReviewGameChangeRequestRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetChangeRequestForReviewAsync(id, cancellationToken);
        if (access.Error is not null) return Result.Failure<GameChangeRequestDto>(access.Error);
        if (access.Item!.Status != GameChangeRequestStatus.Pending) return Result.Failure<GameChangeRequestDto>(ApplicationErrors.ChangeRequestNotPending);
        try
        {
            access.Item.Reject(access.Administrator!.Id, request.Comment ?? string.Empty, Now());
            await AddAuditAsync(access.Administrator.Id, "GameChangeRequestRejected", "GameChangeRequest", id,
                null, JsonSerializer.Serialize(new { access.Item.Status, access.Item.AdminComment }, JsonOptions), cancellationToken);
            await AddSyncEventAsync("user", access.Item.ProposedByUserId, "changeRequestChanged", access.Item.Id,
                new { access.Item.Id, Status = access.Item.Status.ToString() }, cancellationToken);
            await notificationWriter.CreateAsync(access.Item.ProposedByUserId, NotificationTypes.SuggestedEditRejected,
                new { ChangeRequestId = access.Item.Id, access.Item.GameId, access.Item.AdminComment }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(MapChangeRequest(access.Item));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameChangeRequestDto>(ApplicationErrors.Validation(exception.Message)); }
    }

    private async Task<Result<GameSubmissionDto>> ModerateSubmissionAsync(Guid id, ModerateSubmissionRequest request,
        string action, Action<Game, Guid, DateTime> decision, CancellationToken cancellationToken)
    {
        var administrator = await GetProfileAsync(cancellationToken);
        if (administrator is null) return Result.Failure<GameSubmissionDto>(ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetByIdAsync(id, cancellationToken);
        if (game?.SubmittedByUserId is null) return Result.Failure<GameSubmissionDto>(ApplicationErrors.SubmissionNotFound);
        if (game.ModerationStatus != ModerationStatus.Pending) return Result.Failure<GameSubmissionDto>(ApplicationErrors.SubmissionNotPending);
        if (game.Revision != request.ExpectedRevision) return Result.Failure<GameSubmissionDto>(ApplicationErrors.RevisionConflict);
        try
        {
            var before = AuditGame(game); decision(game, administrator.Id, Now());
            await AddAuditAsync(administrator.Id, action, "Game", game.Id, before, AuditGame(game), cancellationToken);
            if (game.ModerationStatus == ModerationStatus.Approved)
                await AddSyncEventAsync("catalog", null, "gameChanged", game.Id,
                    new { game.Id, game.Revision, ModerationStatus = game.ModerationStatus.ToString() }, cancellationToken);
            await AddSyncEventAsync("user", game.SubmittedByUserId!.Value, "submissionChanged", game.Id,
                new { game.Id, game.Revision, ModerationStatus = game.ModerationStatus.ToString(), game.ModerationComment }, cancellationToken);
            var notificationType = game.ModerationStatus switch
            {
                ModerationStatus.Approved => NotificationTypes.GameSubmissionApproved,
                ModerationStatus.NeedsChanges => NotificationTypes.GameSubmissionNeedsChanges,
                _ => NotificationTypes.GameSubmissionRejected
            };
            await notificationWriter.CreateAsync(game.SubmittedByUserId!.Value, notificationType,
                new { GameId = game.Id, game.ModerationComment }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success(MapSubmission(game));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameSubmissionDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConcurrencyException) { return Result.Failure<GameSubmissionDto>(ApplicationErrors.RevisionConflict); }
    }

    private async Task<(Game? Game, ApplicationError? Error)> GetOwnedSubmissionAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return (null, ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetByIdAsync(id, cancellationToken);
        return game?.SubmittedByUserId == profile.Id ? (game, null) : (null, ApplicationErrors.SubmissionNotFound);
    }

    private async Task<(GameChangeRequest? Item, UserProfile? Administrator, ApplicationError? Error)> GetChangeRequestForReviewAsync(Guid id, CancellationToken cancellationToken)
    {
        var administrator = await GetProfileAsync(cancellationToken);
        if (administrator is null) return (null, null, ApplicationErrors.ProfileNotFound);
        var item = await changeRequests.GetByIdAsync(id, cancellationToken);
        return item is null ? (null, administrator, ApplicationErrors.ChangeRequestNotFound) : (item, administrator, null);
    }

    private async Task<ApplicationError?> ValidateReferencesAsync(UpsertGameSubmissionRequest request, CancellationToken cancellationToken)
    {
        var languages = await catalog.GetLanguagesAsync(cancellationToken); var tags = await catalog.GetTagsAsync(cancellationToken);
        return request.LanguageIds.All(id => languages.Any(item => item.Id == id)) && request.TagIds.All(id => tags.Any(item => item.Id == id))
            ? null : ApplicationErrors.InvalidReferenceData;
    }

    private async Task AddAuditAsync(Guid actorId, string action, string entityType, Guid entityId,
        string? before, string? after, CancellationToken cancellationToken) =>
        await auditLogs.AddAsync(AuditLog.Create(Guid.NewGuid(), actorId, action, entityType, entityId, Now(),
            auditContext.CorrelationId, auditContext.DeviceId, auditContext.IpAddress, before, after), cancellationToken);

    private async Task AddSyncEventAsync(string scopeType, Guid? scopeId, string operation, Guid entityId,
        object payload, CancellationToken cancellationToken) =>
        await sync.AddEventAsync(SyncEvent.Create(scopeType, scopeId, operation, entityId,
            JsonSerializer.Serialize(payload, JsonOptions), Now()), cancellationToken);

    private Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(
        currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private static (ModerationStatus? Status, ApplicationError? Error) ParseModerationStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, null);
        return Enum.TryParse<ModerationStatus>(value, true, out var parsed) && parsed != ModerationStatus.Draft
            ? (parsed, null) : (null, ApplicationErrors.Validation("The moderation status is invalid."));
    }
    private static bool IsEmpty(GameChangePatchDto patch) => patch is
        { Title: null, Description: null, Publisher: null, ReleaseYear: null, MinimumPlayers: null,
          MaximumPlayers: null, MinimumAge: null, MinimumPlayingTimeMinutes: null, MaximumPlayingTimeMinutes: null };
    private static void ValidatePatch(GameChangePatchDto patch)
    {
        if (patch.Title is not null && string.IsNullOrWhiteSpace(patch.Title)) throw new DomainValidationException("The proposed title is invalid.");
        if (patch.Description?.Length > 4000 || patch.Publisher?.Length > 200) throw new DomainValidationException("A proposed field is too long.");
    }
    private static GameChangePatchDto DeserializePatch(GameChangeRequest item) =>
        JsonSerializer.Deserialize<GameChangePatchDto>(item.ProposedChangesJson, JsonOptions)
        ?? throw new DomainValidationException("The proposed changes are invalid.");
    private static string AuditGame(Game game) => JsonSerializer.Serialize(new
    { game.Title, game.Publisher, game.ModerationStatus, game.Revision, game.ModerationComment }, JsonOptions);
    private static GameSubmissionDto MapSubmission(Game game) => new(MapGame(game), game.SubmittedByUserId!.Value,
        game.ModerationComment, game.ApprovedByUserId, game.ApprovedAtUtc, game.CreatedAtUtc, game.UpdatedAtUtc);
    private static GameDto MapGame(Game game) => new(game.Id, game.Title, game.Description, game.Publisher, game.ReleaseYear,
        game.MinimumPlayers, game.MaximumPlayers, game.MinimumAge, game.MinimumPlayingTimeMinutes,
        game.MaximumPlayingTimeMinutes, game.ModerationStatus.ToString(), game.Revision,
        game.Barcodes.Select(item => item.NormalizedBarcode).ToList(),
        game.Languages.Select(item => new ReferenceDataDto(item.Language.Id, item.Language.Name, item.Language.Code)).OrderBy(item => item.Name).ToList(),
        game.Tags.Select(item => new ReferenceDataDto(item.Tag.Id, item.Tag.Name)).OrderBy(item => item.Name).ToList());
    private static GameChangeRequestDto MapChangeRequest(GameChangeRequest item) => new(item.Id, item.GameId,
        item.Game.Title, item.ProposedByUserId, DeserializePatch(item), item.Status.ToString(), item.AdminComment,
        item.ReviewedByUserId, item.ReviewedAtUtc, item.CreatedAtUtc, item.UpdatedAtUtc);
}
