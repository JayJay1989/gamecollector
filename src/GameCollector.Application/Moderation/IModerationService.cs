using GameCollector.Application.Common;
using GameCollector.Contracts.Catalog;

namespace GameCollector.Application.Moderation;

public interface IModerationService
{
    Task<Result<GameSubmissionDto>> CreateSubmissionAsync(UpsertGameSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GameSubmissionDto>>> GetMySubmissionsAsync(CancellationToken cancellationToken = default);
    Task<Result<GameSubmissionDto>> GetMySubmissionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameSubmissionDto>> UpdateSubmissionAsync(Guid id, UpsertGameSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteSubmissionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameSubmissionDto>> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameChangeRequestDto>> CreateChangeRequestAsync(Guid gameId, CreateGameChangeRequestRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GameChangeRequestDto>>> GetMyChangeRequestsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GameSubmissionDto>>> GetModerationQueueAsync(string? status, CancellationToken cancellationToken = default);
    Task<Result<GameSubmissionDto>> GetSubmissionForModerationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameSubmissionDto>> ApproveSubmissionAsync(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<Result<GameSubmissionDto>> RequestSubmissionChangesAsync(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<Result<GameSubmissionDto>> RejectSubmissionAsync(Guid id, ModerateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GameChangeRequestDto>>> GetChangeRequestQueueAsync(string? status, CancellationToken cancellationToken = default);
    Task<Result<GameChangeRequestDto>> ApproveChangeRequestAsync(Guid id, ReviewGameChangeRequestRequest request, CancellationToken cancellationToken = default);
    Task<Result<GameChangeRequestDto>> RejectChangeRequestAsync(Guid id, ReviewGameChangeRequestRequest request, CancellationToken cancellationToken = default);
}
