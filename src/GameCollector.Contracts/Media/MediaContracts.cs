namespace GameCollector.Contracts.Media;

public sealed record CreateUploadIntentRequest(Guid GameId, string ImageType, string ContentType, long FileSizeBytes);
public sealed record UploadIntentDto(Guid MediaId, Uri UploadUrl, DateTime ExpiresAtUtc);
public sealed record GameImageDto(Guid Id, Guid GameId, string ImageType, string Status, string ContentType,
    long? FileSizeBytes, int? Width, int? Height, string? Checksum, Uri? OriginalUrl, Uri? ThumbnailUrl);

public sealed record ProductMetadataCandidateDto(string Barcode, string Source, Guid? ExistingGameId,
    string? Title, string? Publisher, string? Description);
