using GameCollector.Domain.Users;

namespace GameCollector.Application.Abstractions.Persistence;

public interface IDeviceRegistrationRepository : IRepository<DeviceRegistration, Guid>
{
    Task<DeviceRegistration?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
