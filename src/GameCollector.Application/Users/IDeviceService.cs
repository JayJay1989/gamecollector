using GameCollector.Application.Common;
using GameCollector.Contracts.Users;

namespace GameCollector.Application.Users;

public interface IDeviceService
{
    Task<Result<DeviceRegistrationDto>> ActivateAsync(
        ActivateDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RevokeAsync(CancellationToken cancellationToken = default);

    Task<bool> IsActiveAsync(
        string identitySubject,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}
