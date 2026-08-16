using GameCollector.Application.Common;
using GameCollector.Contracts.Media;

namespace GameCollector.Application.Media;

public interface IMediaService
{
    Task<Result<UploadIntentDto>> CreateUploadIntentAsync(CreateUploadIntentRequest request, CancellationToken cancellationToken = default);
    Task<Result<GameImageDto>> CompleteAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<Result<GameImageDto>> GetAsync(Guid mediaId, CancellationToken cancellationToken = default);
}
