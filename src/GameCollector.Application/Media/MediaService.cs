using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Media;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Media;
using GameCollector.Domain.Catalog;
using GameCollector.Domain.Users;
using GameCollector.Application.Abstractions.Background;
using System.Text.Json;

namespace GameCollector.Application.Media;

public sealed class MediaService(
    ICurrentUser currentUser,
    IUserProfileRepository users,
    ICatalogRepository catalog,
    IGameImageRepository images,
    IObjectStorage storage,
    IImageProcessor imageProcessor,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IMediaService
{
    public const long MaximumFileSizeBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);
    private static readonly HashSet<string> AcceptedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public async Task<Result<UploadIntentDto>> CreateUploadIntentAsync(CreateUploadIntentRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<GameImageType>(request.ImageType, true, out var imageType) ||
            !AcceptedContentTypes.Contains(request.ContentType) || request.FileSizeBytes is < 1 or > MaximumFileSizeBytes)
            return Result.Failure<UploadIntentDto>(ApplicationErrors.InvalidMediaRequest);

        var access = await GetWriteAccessAsync(request.GameId, cancellationToken);
        if (access.Error is not null) return Result.Failure<UploadIntentDto>(access.Error);
        if (!currentUser.IsAdministrator && access.Game!.ModerationStatus is not (ModerationStatus.Draft or ModerationStatus.NeedsChanges))
            return Result.Failure<UploadIntentDto>(ApplicationErrors.SubmissionNotEditable);

        var existing = await images.GetByGameAndTypeAsync(request.GameId, imageType, cancellationToken);
        var mediaId = existing?.Id ?? Guid.NewGuid();
        var extension = request.ContentType.ToLowerInvariant() switch { "image/png" => "png", "image/webp" => "webp", _ => "jpg" };
        var objectKey = $"games/{request.GameId:N}/{imageType.ToString().ToLowerInvariant()}/{mediaId:N}-{Guid.NewGuid():N}.{extension}";
        var expiresAt = Now().Add(UploadLifetime);
        var uploadUrl = await storage.CreateUploadUrlAsync(objectKey, UploadLifetime, cancellationToken);
        if (existing is null)
            await images.AddAsync(GameImage.Create(mediaId, request.GameId, imageType, objectKey,
                request.ContentType.ToLowerInvariant(), request.FileSizeBytes, Now()), cancellationToken);
        else
            existing.RestartUpload(objectKey, request.ContentType.ToLowerInvariant(), request.FileSizeBytes, Now());
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PersistenceConflictException exception) when (exception.Constraint == PersistenceConstraints.GameImageType)
        { return Result.Failure<UploadIntentDto>(ApplicationErrors.MediaAlreadyExists); }
        return Result.Success(new UploadIntentDto(mediaId, uploadUrl, expiresAt));
    }

    public async Task<Result<GameImageDto>> CompleteAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var image = await images.GetByIdAsync(mediaId, cancellationToken);
        if (image is null) return Result.Failure<GameImageDto>(ApplicationErrors.MediaNotFound);
        var access = await GetWriteAccessAsync(image.GameId, cancellationToken);
        if (access.Error is not null) return Result.Failure<GameImageDto>(access.Error);
        if (image.Status != GameImageStatus.PendingUpload) return Result.Failure<GameImageDto>(ApplicationErrors.UploadNotPending);

        byte[] content;
        try { content = await storage.ReadAsync(image.OriginalObjectKey, MaximumFileSizeBytes, cancellationToken); }
        catch (ObjectNotFoundException) { return Result.Failure<GameImageDto>(ApplicationErrors.UploadNotFound); }
        catch (InvalidDataException) { return Result.Failure<GameImageDto>(ApplicationErrors.InvalidImage); }
        if (content.LongLength != image.RequestedFileSizeBytes)
            return Result.Failure<GameImageDto>(ApplicationErrors.InvalidImage);
        ValidatedImage validated;
        try { validated = imageProcessor.Validate(content); }
        catch (InvalidDataException) { return Result.Failure<GameImageDto>(ApplicationErrors.InvalidImage); }
        if (!string.Equals(image.ContentType, validated.ContentType, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<GameImageDto>(ApplicationErrors.InvalidImage);

        image.MarkProcessing(validated.ContentType, content.LongLength, validated.Width, validated.Height, validated.Checksum, Now());
        await outbox.EnqueueAsync(OutboxMessageTypes.GenerateThumbnail,
            JsonSerializer.Serialize(new { MediaId = image.Id }), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(image));
    }

    public async Task<Result<GameImageDto>> UploadAsync(Guid mediaId, string? contentType, ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var image = await images.GetByIdAsync(mediaId, cancellationToken);
        if (image is null) return Result.Failure<GameImageDto>(ApplicationErrors.MediaNotFound);
        var access = await GetWriteAccessAsync(image.GameId, cancellationToken);
        if (access.Error is not null) return Result.Failure<GameImageDto>(access.Error);
        if (image.Status != GameImageStatus.PendingUpload) return Result.Failure<GameImageDto>(ApplicationErrors.UploadNotPending);
        if (content.IsEmpty || content.Length > MaximumFileSizeBytes || content.Length != image.RequestedFileSizeBytes ||
            !string.Equals(contentType, image.ContentType, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<GameImageDto>(ApplicationErrors.InvalidImage);

        ValidatedImage validated;
        try { validated = imageProcessor.Validate(content); }
        catch (InvalidDataException) { return Result.Failure<GameImageDto>(ApplicationErrors.InvalidImage); }
        if (!string.Equals(image.ContentType, validated.ContentType, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<GameImageDto>(ApplicationErrors.InvalidImage);

        await storage.WriteAsync(image.OriginalObjectKey, content, validated.ContentType, cancellationToken);
        image.MarkProcessing(validated.ContentType, content.Length, validated.Width, validated.Height, validated.Checksum, Now());
        await outbox.EnqueueAsync(OutboxMessageTypes.GenerateThumbnail,
            JsonSerializer.Serialize(new { MediaId = image.Id }), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(image));
    }

    public async Task<Result<GameImageDto>> GetAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var image = await images.GetByIdAsync(mediaId, cancellationToken);
        if (image is null) return Result.Failure<GameImageDto>(ApplicationErrors.MediaNotFound);
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<GameImageDto>(ApplicationErrors.ProfileNotFound);
        if (await catalog.GetVisibleByIdAsync(image.GameId, profile.Id, currentUser.IsAdministrator, cancellationToken) is null)
            return Result.Failure<GameImageDto>(ApplicationErrors.MediaNotFound);
        Uri? thumbnail = null;
        if (image.Status == GameImageStatus.Ready)
        {
            thumbnail = await storage.CreateDownloadUrlAsync(image.ThumbnailObjectKey!, DownloadLifetime, cancellationToken);
        }
        return Result.Success(Map(image, thumbnail));
    }

    public async Task<Result<IReadOnlyList<GameImageDto>>> ListForGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<GameImageDto>>(ApplicationErrors.ProfileNotFound);
        if (await catalog.GetVisibleByIdAsync(gameId, profile.Id, currentUser.IsAdministrator, cancellationToken) is null)
            return Result.Failure<IReadOnlyList<GameImageDto>>(ApplicationErrors.GameNotFound);

        var items = await images.GetForGameAsync(gameId, cancellationToken);
        return Result.Success<IReadOnlyList<GameImageDto>>(items
            .OrderBy(item => item.ImageType)
            .Select(item => Map(item))
            .ToList());
    }

    public async Task<Result<ThumbnailContent>> GetThumbnailAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var image = await images.GetByIdAsync(mediaId, cancellationToken);
        if (image is null || image.Status != GameImageStatus.Ready || string.IsNullOrWhiteSpace(image.ThumbnailObjectKey))
            return Result.Failure<ThumbnailContent>(ApplicationErrors.MediaNotFound);
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<ThumbnailContent>(ApplicationErrors.ProfileNotFound);
        if (await catalog.GetVisibleByIdAsync(image.GameId, profile.Id, currentUser.IsAdministrator, cancellationToken) is null)
            return Result.Failure<ThumbnailContent>(ApplicationErrors.MediaNotFound);

        try
        {
            var content = await storage.ReadAsync(image.ThumbnailObjectKey, MaximumFileSizeBytes, cancellationToken);
            return Result.Success(new ThumbnailContent(content, "image/jpeg"));
        }
        catch (ObjectNotFoundException)
        {
            return Result.Failure<ThumbnailContent>(ApplicationErrors.MediaNotFound);
        }
    }

    private async Task<(Game? Game, ApplicationError? Error)> GetWriteAccessAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return (null, ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetByIdAsync(gameId, cancellationToken);
        if (game is null) return (null, ApplicationErrors.GameNotFound);
        return currentUser.IsAdministrator || game.SubmittedByUserId == profile.Id
            ? (game, null)
            : (game, ApplicationErrors.MediaAccessDenied);
    }

    private Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken) =>
        users.GetBySubjectAsync(currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
    private static GameImageDto Map(GameImage image, Uri? thumbnail = null) =>
        new(image.Id, image.GameId, image.ImageType.ToString(), image.Status.ToString(), image.ContentType,
            image.FileSizeBytes, image.Width, image.Height, image.Checksum, null, thumbnail);
}
