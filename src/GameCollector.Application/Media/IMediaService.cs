using GameCollector.Application.Common;
using GameCollector.Contracts.Media;

namespace GameCollector.Application.Media;

public interface IMediaService
{
    Task<Result<UploadIntentDto>> CreateUploadIntentAsync(CreateUploadIntentRequest request, CancellationToken cancellationToken = default);
    Task<Result<GameImageDto>> UploadAsync(Guid mediaId, string? contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task<Result<GameImageDto>> CompleteAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<Result<GameImageDto>> GetAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GameImageDto>>> ListForGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<Result<ThumbnailContent>> GetThumbnailAsync(Guid mediaId, CancellationToken cancellationToken = default);
}

public sealed record ThumbnailContent(byte[] Content, string ContentType);
