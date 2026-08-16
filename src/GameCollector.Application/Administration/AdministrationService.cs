using System.Text.Json;
using GameCollector.Application.Abstractions.Auditing;
using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Application.Notifications;
using GameCollector.Application.Sync;
using GameCollector.Contracts.Admin;
using GameCollector.Contracts.Catalog;
using GameCollector.Contracts.Notifications;
using GameCollector.Domain.Auditing;
using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;

namespace GameCollector.Application.Administration;

public sealed class AdministrationService(
    ICurrentUser currentUser, IAuditContext auditContext, IUserProfileRepository users,
    IDeviceRegistrationRepository devices, ICollectionRepository collections,
    ICollectionGameRepository collectionGames, ICatalogRepository catalog,
    IAuditLogRepository auditLogs, ISyncDiagnosticRepository diagnostics,
    ISyncEventWriter syncEvents, INotificationWriter notifications,
    IUnitOfWork unitOfWork, TimeProvider timeProvider) : IAdministrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<AdminUserSummaryDto>>> SearchUsersAsync(string? query, int limit,
        CancellationToken cancellationToken = default)
    {
        if (!ValidLimit(limit)) return Result.Failure<IReadOnlyList<AdminUserSummaryDto>>(ApplicationErrors.Validation("Limit must be between 1 and 200."));
        var items = await users.SearchForAdministrationAsync(query, limit, cancellationToken);
        return Result.Success<IReadOnlyList<AdminUserSummaryDto>>(items.Select(MapUser).ToList());
    }

    public async Task<Result<AdminUserDetailDto>> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null) return Result.Failure<AdminUserDetailDto>(ApplicationErrors.AdminUserNotFound);
        var device = await devices.GetByUserIdAsync(id, cancellationToken);
        var accessible = await collections.GetForUserAsync(id, cancellationToken);
        var submissions = await catalog.GetSubmissionsForUserAsync(id, cancellationToken);
        return Result.Success(new AdminUserDetailDto(MapUser(user), device is null ? null :
            new AdminDeviceDto(device.DeviceId, device.ActivatedAtUtc, device.LastSeenAtUtc),
            accessible.Select(item => new AdminUserCollectionDto(item.Id, item.Name,
                item.OwnerUserId == id ? "Owner" : item.GetMemberRole(id)!.Value.ToString(), item.OwnerUserId == id)).ToList(),
            submissions.Select(MapSubmission).ToList()));
    }

    public async Task<Result<bool>> SetUserDisabledAsync(Guid id, bool disabled, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(cancellationToken);
        if (actor is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        if (actor.Id == id && disabled) return Result.Failure<bool>(ApplicationErrors.AdminCannotDisableSelf);
        var target = await users.GetByIdAsync(id, cancellationToken);
        if (target is null) return Result.Failure<bool>(ApplicationErrors.AdminUserNotFound);
        if (target.IsDisabled == disabled) return Result.Success(true);
        var before = JsonSerializer.Serialize(new { target.IsDisabled }, JsonOptions);
        if (disabled) target.Disable(Now()); else target.Enable(Now());
        await syncEvents.WriteAsync("user", target.Id, "profileChanged", target.Id,
            new { target.Id, target.IsDisabled, target.UpdatedAtUtc }, cancellationToken);
        await AddAuditAsync(actor.Id, disabled ? "UserDisabled" : "UserEnabled", "UserProfile", target.Id,
            before, JsonSerializer.Serialize(new { target.IsDisabled }, JsonOptions), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    public async Task<Result<bool>> RevokeDeviceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(cancellationToken);
        if (actor is null) return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        var target = await users.GetByIdAsync(id, cancellationToken);
        if (target is null) return Result.Failure<bool>(ApplicationErrors.AdminUserNotFound);
        var device = await devices.GetByUserIdAsync(id, cancellationToken);
        if (device is null) return Result.Success(true);
        var deviceId = device.DeviceId;
        await notifications.CreateAsync(id, NotificationTypes.DeviceRegistrationRevoked,
            new { DeviceId = deviceId, RevokedByAdministrator = true }, cancellationToken);
        devices.Remove(device);
        await AddAuditAsync(actor.Id, "DeviceRevoked", "UserProfile", id, null,
            JsonSerializer.Serialize(new { DeviceId = deviceId }, JsonOptions), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    public async Task<Result<IReadOnlyList<AdminCollectionSummaryDto>>> SearchCollectionsAsync(string? query, int limit,
        CancellationToken cancellationToken = default)
    {
        if (!ValidLimit(limit)) return Result.Failure<IReadOnlyList<AdminCollectionSummaryDto>>(ApplicationErrors.Validation("Limit must be between 1 and 200."));
        var items = await collections.SearchForAdministrationAsync(query, limit, cancellationToken);
        var result = new List<AdminCollectionSummaryDto>(items.Count);
        foreach (var item in items)
        {
            var games = await collectionGames.GetForCollectionAsync(item.Id, cancellationToken);
            result.Add(new AdminCollectionSummaryDto(item.Id, item.Name, item.OwnerUserId,
                item.Members.Count + 1, games.Count, item.UpdatedAtUtc));
        }
        return Result.Success<IReadOnlyList<AdminCollectionSummaryDto>>(result);
    }

    public async Task<Result<AdminCollectionDetailDto>> GetCollectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await collections.GetByIdAsync(id, cancellationToken);
        if (item is null) return Result.Failure<AdminCollectionDetailDto>(ApplicationErrors.CollectionNotFound);
        var ids = item.Members.Select(member => member.UserId).Append(item.OwnerUserId).ToArray();
        var profiles = await users.GetByIdsAsync(ids, cancellationToken);
        var games = await collectionGames.GetForCollectionAsync(id, cancellationToken);
        var members = profiles.Select(profile => new AdminCollectionMemberDto(profile.Id, profile.DisplayName,
            profile.Username, profile.Id == item.OwnerUserId ? "Owner" : item.GetMemberRole(profile.Id)!.Value.ToString(),
            item.Members.SingleOrDefault(member => member.UserId == profile.Id)?.JoinedAtUtc)).ToList();
        return Result.Success(new AdminCollectionDetailDto(item.Id, item.Name, item.OwnerUserId, members,
            games.Select(game => new AdminCollectionGameDto(game.GameId, game.Game.Title, game.AddedAtUtc)).ToList(),
            item.CreatedAtUtc, item.UpdatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<GameDto>>> ListGamesAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        if (!ValidLimit(limit)) return Result.Failure<IReadOnlyList<GameDto>>(ApplicationErrors.Validation("Limit must be between 1 and 200."));
        var actor = await GetActorAsync(cancellationToken);
        if (actor is null) return Result.Failure<IReadOnlyList<GameDto>>(ApplicationErrors.ProfileNotFound);
        var items = await catalog.SearchVisibleAsync(query, actor.Id, true, limit, cancellationToken);
        return Result.Success<IReadOnlyList<GameDto>>(items.Select(MapGame).ToList());
    }

    public async Task<Result<GameDto>> GetGameAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var game = await catalog.GetByIdAsync(id, cancellationToken);
        return game is null ? Result.Failure<GameDto>(ApplicationErrors.GameNotFound) : Result.Success(MapGame(game));
    }

    public async Task<Result<GameDto>> CreateGameAsync(AdminGameRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(cancellationToken);
        if (actor is null) return Result.Failure<GameDto>(ApplicationErrors.ProfileNotFound);
        var referenceError = await ValidateReferencesAsync(request, cancellationToken);
        if (referenceError is not null) return Result.Failure<GameDto>(referenceError);
        try
        {
            var game = Game.CreateApproved(Guid.NewGuid(), request.Title, request.Description, request.Publisher,
                request.ReleaseYear, request.MinimumPlayers, request.MaximumPlayers, request.MinimumAge,
                request.MinimumPlayingTimeMinutes, request.MaximumPlayingTimeMinutes, actor.Id, Now());
            ApplyReferences(game, request);
            await catalog.AddAsync(game, cancellationToken);
            await syncEvents.WriteAsync("catalog", null, "gameChanged", game.Id,
                new { game.Id, game.Revision, ModerationStatus = game.ModerationStatus.ToString() }, cancellationToken);
            await AddAuditAsync(actor.Id, "GameCreated", "Game", game.Id, null, AuditGame(game), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(MapGame(game));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConflictException) { return Result.Failure<GameDto>(ApplicationErrors.Validation("A barcode is already assigned to another game.")); }
    }

    public async Task<Result<GameDto>> UpdateGameAsync(Guid id, AdminGameRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(cancellationToken);
        if (actor is null) return Result.Failure<GameDto>(ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetByIdAsync(id, cancellationToken);
        if (game is null) return Result.Failure<GameDto>(ApplicationErrors.GameNotFound);
        if (!request.ExpectedRevision.HasValue || request.ExpectedRevision.Value != game.Revision)
            return Result.Failure<GameDto>(ApplicationErrors.RevisionConflict);
        var referenceError = await ValidateReferencesAsync(request, cancellationToken);
        if (referenceError is not null) return Result.Failure<GameDto>(referenceError);
        try
        {
            var before = AuditGame(game);
            game.UpdateApproved(request.Title, request.Description, request.Publisher, request.ReleaseYear,
                request.MinimumPlayers, request.MaximumPlayers, request.MinimumAge,
                request.MinimumPlayingTimeMinutes, request.MaximumPlayingTimeMinutes,
                request.Barcodes, request.LanguageIds, request.TagIds, Now());
            await syncEvents.WriteAsync("catalog", null, "gameChanged", game.Id,
                new { game.Id, game.Revision, ModerationStatus = game.ModerationStatus.ToString() }, cancellationToken);
            await AddAuditAsync(actor.Id, "GameUpdated", "Game", game.Id, before, AuditGame(game), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(MapGame(game));
        }
        catch (DomainValidationException exception) { return Result.Failure<GameDto>(ApplicationErrors.Validation(exception.Message)); }
        catch (PersistenceConcurrencyException) { return Result.Failure<GameDto>(ApplicationErrors.RevisionConflict); }
        catch (PersistenceConflictException) { return Result.Failure<GameDto>(ApplicationErrors.Validation("A barcode is already assigned to another game.")); }
    }

    public async Task<Result<IReadOnlyList<AdminAuditDto>>> SearchAuditAsync(string? action, string? entityType,
        Guid? entityId, Guid? actorUserId, DateTime? fromUtc, DateTime? toUtc, int limit,
        CancellationToken cancellationToken = default)
    {
        if (!ValidLimit(limit) || (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc))
            return Result.Failure<IReadOnlyList<AdminAuditDto>>(ApplicationErrors.Validation("The audit query is invalid."));
        var items = await auditLogs.SearchAsync(action, entityType, entityId, actorUserId, fromUtc, toUtc, limit, cancellationToken);
        return Result.Success<IReadOnlyList<AdminAuditDto>>(items.Select(item => new AdminAuditDto(item.Id,
            item.ActorUserId, item.Action, item.EntityType, item.EntityId, item.TimestampUtc, item.CorrelationId,
            item.DeviceId, item.IpAddress, Parse(item.BeforeJson), Parse(item.AfterJson))).ToList());
    }

    public async Task<Result<IReadOnlyList<SyncDiagnosticDto>>> GetSyncDiagnosticsAsync(Guid? userId, int limit,
        CancellationToken cancellationToken = default)
    {
        if (!ValidLimit(limit)) return Result.Failure<IReadOnlyList<SyncDiagnosticDto>>(ApplicationErrors.Validation("Limit must be between 1 and 200."));
        var items = await diagnostics.SearchAsync(userId, limit, cancellationToken);
        return Result.Success<IReadOnlyList<SyncDiagnosticDto>>(items.Select(item => new SyncDiagnosticDto(item.UserId,
            item.DeviceId, item.LastSuccessfulSyncAtUtc, item.LastCursor, item.UploadedMutations,
            item.DownloadedEvents, item.LastError, item.LastErrorAtUtc)).ToList());
    }

    private async Task<ApplicationError?> ValidateReferencesAsync(AdminGameRequest request, CancellationToken cancellationToken)
    {
        var languages = await catalog.GetLanguagesAsync(cancellationToken);
        var tags = await catalog.GetTagsAsync(cancellationToken);
        return request.LanguageIds.All(id => languages.Any(item => item.Id == id)) &&
               request.TagIds.All(id => tags.Any(item => item.Id == id))
            ? null : ApplicationErrors.InvalidReferenceData;
    }

    private static void ApplyReferences(Game game, AdminGameRequest request)
    {
        foreach (var barcode in request.Barcodes.Distinct(StringComparer.Ordinal)) game.AddBarcode(Guid.NewGuid(), barcode);
        foreach (var id in request.LanguageIds.Distinct()) game.AddLanguage(id);
        foreach (var id in request.TagIds.Distinct()) game.AddTag(id);
    }

    private async Task AddAuditAsync(Guid actorId, string action, string entityType, Guid entityId,
        string? before, string? after, CancellationToken cancellationToken) =>
        await auditLogs.AddAsync(AuditLog.Create(Guid.NewGuid(), actorId, action, entityType, entityId, Now(),
            auditContext.CorrelationId, auditContext.DeviceId, auditContext.IpAddress, before, after), cancellationToken);

    private Task<Domain.Users.UserProfile?> GetActorAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(
        currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private static bool ValidLimit(int limit) => limit is >= 1 and <= 200;
    private static AdminUserSummaryDto MapUser(Domain.Users.UserProfile item) =>
        new(item.Id, item.DisplayName, item.Username, item.IsDisabled, item.CreatedAtUtc, item.UpdatedAtUtc);
    private static GameSubmissionDto MapSubmission(Game game) => new(MapGame(game), game.SubmittedByUserId!.Value,
        game.ModerationComment, game.ApprovedByUserId, game.ApprovedAtUtc, game.CreatedAtUtc, game.UpdatedAtUtc);
    private static GameDto MapGame(Game game) => new(game.Id, game.Title, game.Description, game.Publisher,
        game.ReleaseYear, game.MinimumPlayers, game.MaximumPlayers, game.MinimumAge,
        game.MinimumPlayingTimeMinutes, game.MaximumPlayingTimeMinutes, game.ModerationStatus.ToString(), game.Revision,
        game.Barcodes.Select(item => item.NormalizedBarcode).ToList(),
        game.Languages.Select(item => new ReferenceDataDto(item.Language.Id, item.Language.Name, item.Language.Code)).OrderBy(item => item.Name).ToList(),
        game.Tags.Select(item => new ReferenceDataDto(item.Tag.Id, item.Tag.Name)).OrderBy(item => item.Name).ToList());
    private static string AuditGame(Game game) => JsonSerializer.Serialize(new
    { game.Title, game.Publisher, game.ModerationStatus, game.Revision, Barcodes = game.Barcodes.Select(item => item.NormalizedBarcode) }, JsonOptions);
    private static JsonElement? Parse(string? value) => string.IsNullOrWhiteSpace(value)
        ? null : JsonSerializer.Deserialize<JsonElement>(value, JsonOptions);
}
