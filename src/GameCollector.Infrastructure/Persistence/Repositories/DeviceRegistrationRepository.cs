using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class DeviceRegistrationRepository(ApplicationDbContext dbContext)
    : IDeviceRegistrationRepository
{
    public Task<DeviceRegistration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.DeviceRegistrations.SingleOrDefaultAsync(
            device => device.DeviceId == id,
            cancellationToken);

    public Task<DeviceRegistration?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.DeviceRegistrations.SingleOrDefaultAsync(
            device => device.UserId == userId,
            cancellationToken);

    public async Task AddAsync(
        DeviceRegistration entity,
        CancellationToken cancellationToken = default) =>
        await dbContext.DeviceRegistrations.AddAsync(entity, cancellationToken);

    public void Remove(DeviceRegistration entity) => dbContext.DeviceRegistrations.Remove(entity);
}
