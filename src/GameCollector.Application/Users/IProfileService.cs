using GameCollector.Application.Common;
using GameCollector.Contracts.Users;

namespace GameCollector.Application.Users;

public interface IProfileService
{
    Task<Result<UserProfileDto>> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<Result<UserProfileDto>> OnboardAsync(
        OnboardUserRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UserProfileDto>> UpdateAsync(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default);
}
