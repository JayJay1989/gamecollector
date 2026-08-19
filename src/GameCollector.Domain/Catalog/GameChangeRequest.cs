using GameCollector.Domain.Common;

namespace GameCollector.Domain.Catalog;

public enum GameChangeRequestStatus { Pending, Approved, Rejected }

public sealed class GameChangeRequest
{
    private readonly List<GameChangeRequestImage> _images = [];
    private GameChangeRequest() { }
    private GameChangeRequest(Guid id, Guid gameId, Guid proposedByUserId, string proposedChangesJson, DateTime createdAtUtc)
    {
        Id = id; GameId = gameId; ProposedByUserId = proposedByUserId;
        ProposedChangesJson = Required(proposedChangesJson, 16000, "Proposed changes");
        Status = GameChangeRequestStatus.Pending;
        CreatedAtUtc = Utc(createdAtUtc); UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public Guid ProposedByUserId { get; private set; }
    public string ProposedChangesJson { get; private set; } = string.Empty;
    public GameChangeRequestStatus Status { get; private set; }
    public string? AdminComment { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Game Game { get; private set; } = null!;
    public IReadOnlyCollection<GameChangeRequestImage> Images => _images;

    public static GameChangeRequest Create(Guid id, Guid gameId, Guid userId, string proposedChangesJson, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || gameId == Guid.Empty || userId == Guid.Empty)
            throw new DomainValidationException("Valid change-request IDs are required.");
        return new GameChangeRequest(id, gameId, userId, proposedChangesJson, createdAtUtc);
    }

    public void Approve(Guid administratorId, string? comment, DateTime reviewedAtUtc) => Review(GameChangeRequestStatus.Approved, administratorId, comment, reviewedAtUtc);
    public void Reject(Guid administratorId, string comment, DateTime reviewedAtUtc) => Review(GameChangeRequestStatus.Rejected, administratorId, Required(comment, 2000, "Rejection reason"), reviewedAtUtc);

    public void AddImage(GameChangeRequestImage image)
    {
        if (Status != GameChangeRequestStatus.Pending) throw new DomainValidationException("The change request has already been reviewed.");
        if (image.ChangeRequestId != Id) throw new DomainValidationException("The proposed image belongs to another change request.");
        if (_images.Any(item => item.ImageType == image.ImageType)) throw new DomainValidationException("An image of that type is already proposed.");
        _images.Add(image);
        UpdatedAtUtc = image.UpdatedAtUtc;
    }

    public void RemoveImage(GameChangeRequestImage image) => _images.Remove(image);

    private void Review(GameChangeRequestStatus status, Guid administratorId, string? comment, DateTime reviewedAtUtc)
    {
        if (Status != GameChangeRequestStatus.Pending) throw new DomainValidationException("The change request has already been reviewed.");
        if (administratorId == Guid.Empty) throw new DomainValidationException("An administrator ID is required.");
        Status = status; ReviewedByUserId = administratorId; AdminComment = Optional(comment, 2000);
        ReviewedAtUtc = Utc(reviewedAtUtc); UpdatedAtUtc = ReviewedAtUtc.Value;
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Required(string value, int max, string name) { var result = value.Trim(); if (result.Length is < 1 || result.Length > max) throw new DomainValidationException($"{name} is invalid."); return result; }
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Required(value, max, "Comment");
}

public sealed class GameChangeRequestImage
{
    private GameChangeRequestImage() { }

    private GameChangeRequestImage(Guid id, Guid changeRequestId, GameImageType imageType, string objectKey,
        string contentType, long fileSizeBytes, int width, int height, string checksum, DateTime createdAtUtc)
    {
        Id = id; ChangeRequestId = changeRequestId; ImageType = imageType;
        ObjectKey = Required(objectKey, 500, "Object key");
        ContentType = Required(contentType, 100, "Content type");
        if (fileSizeBytes < 1 || width < 1 || height < 1) throw new DomainValidationException("The proposed image metadata is invalid.");
        FileSizeBytes = fileSizeBytes; Width = width; Height = height;
        Checksum = Required(checksum, 64, "Checksum");
        CreatedAtUtc = Utc(createdAtUtc); UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ChangeRequestId { get; private set; }
    public GameImageType ImageType { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string Checksum { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public GameChangeRequest ChangeRequest { get; private set; } = null!;

    public static GameChangeRequestImage Create(Guid id, Guid changeRequestId, GameImageType imageType,
        string objectKey, string contentType, long fileSizeBytes, int width, int height, string checksum, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || changeRequestId == Guid.Empty) throw new DomainValidationException("Valid proposed-image IDs are required.");
        return new GameChangeRequestImage(id, changeRequestId, imageType, objectKey, contentType,
            fileSizeBytes, width, height, checksum, createdAtUtc);
    }

    public void Replace(string objectKey, string contentType, long fileSizeBytes, int width, int height,
        string checksum, DateTime updatedAtUtc)
    {
        ObjectKey = Required(objectKey, 500, "Object key");
        ContentType = Required(contentType, 100, "Content type");
        if (fileSizeBytes < 1 || width < 1 || height < 1) throw new DomainValidationException("The proposed image metadata is invalid.");
        FileSizeBytes = fileSizeBytes; Width = width; Height = height;
        Checksum = Required(checksum, 64, "Checksum"); UpdatedAtUtc = Utc(updatedAtUtc);
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Required(string value, int maxLength, string name)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is < 1 || trimmed.Length > maxLength) throw new DomainValidationException($"{name} is invalid.");
        return trimmed;
    }
}
