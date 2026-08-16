using GameCollector.Domain.Common;

namespace GameCollector.Domain.Catalog;

public enum GameImageType
{
    Front,
    Back
}

public enum GameImageStatus
{
    PendingUpload,
    Processing,
    Ready,
    Failed
}

public sealed class GameImage
{
    private GameImage() { }

    private GameImage(Guid id, Guid gameId, GameImageType imageType, string originalObjectKey,
        string requestedContentType, long requestedFileSizeBytes, DateTime createdAtUtc)
    {
        Id = id;
        GameId = gameId;
        ImageType = imageType;
        OriginalObjectKey = Required(originalObjectKey, 500, "Object key");
        ContentType = Required(requestedContentType, 100, "Content type");
        RequestedFileSizeBytes = requestedFileSizeBytes;
        Status = GameImageStatus.PendingUpload;
        CreatedAtUtc = Utc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public GameImageType ImageType { get; private set; }
    public string OriginalObjectKey { get; private set; } = string.Empty;
    public string? ThumbnailObjectKey { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long RequestedFileSizeBytes { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string? Checksum { get; private set; }
    public GameImageStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Game Game { get; private set; } = null!;

    public static GameImage Create(Guid id, Guid gameId, GameImageType imageType,
        string originalObjectKey, string requestedContentType, long requestedFileSizeBytes, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || gameId == Guid.Empty) throw new DomainValidationException("Valid media IDs are required.");
        if (requestedFileSizeBytes < 1) throw new DomainValidationException("The image file size is invalid.");
        return new GameImage(id, gameId, imageType, originalObjectKey, requestedContentType, requestedFileSizeBytes, createdAtUtc);
    }

    public void MarkProcessing(string contentType, long fileSizeBytes, int width, int height, string checksum, DateTime updatedAtUtc)
    {
        if (Status != GameImageStatus.PendingUpload) throw new DomainValidationException("The image upload is no longer pending.");
        ContentType = Required(contentType, 100, "Content type");
        FileSizeBytes = fileSizeBytes;
        Width = width;
        Height = height;
        Checksum = Required(checksum, 64, "Checksum");
        Status = GameImageStatus.Processing;
        UpdatedAtUtc = Utc(updatedAtUtc);
    }

    public void MarkReady(string thumbnailObjectKey, DateTime updatedAtUtc)
    {
        if (Status != GameImageStatus.Processing) throw new DomainValidationException("The image is not processing.");
        ThumbnailObjectKey = Required(thumbnailObjectKey, 500, "Thumbnail object key");
        Status = GameImageStatus.Ready;
        UpdatedAtUtc = Utc(updatedAtUtc);
    }

    public void MarkFailed(DateTime updatedAtUtc)
    {
        if (Status == GameImageStatus.Ready) throw new DomainValidationException("A ready image cannot be failed.");
        Status = GameImageStatus.Failed;
        UpdatedAtUtc = Utc(updatedAtUtc);
    }

    public void RestartUpload(string originalObjectKey, string requestedContentType, long requestedFileSizeBytes, DateTime updatedAtUtc)
    {
        if (requestedFileSizeBytes < 1) throw new DomainValidationException("The image file size is invalid.");
        OriginalObjectKey = Required(originalObjectKey, 500, "Object key");
        ThumbnailObjectKey = null;
        ContentType = Required(requestedContentType, 100, "Content type");
        RequestedFileSizeBytes = requestedFileSizeBytes;
        FileSizeBytes = null; Width = null; Height = null; Checksum = null;
        Status = GameImageStatus.PendingUpload;
        UpdatedAtUtc = Utc(updatedAtUtc);
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Required(string value, int maxLength, string name)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is < 1 || trimmed.Length > maxLength) throw new DomainValidationException($"{name} is invalid.");
        return trimmed;
    }
}
