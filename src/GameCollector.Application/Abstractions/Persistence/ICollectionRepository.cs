using GameCollector.Domain.Collections;

namespace GameCollector.Application.Abstractions.Persistence;

public interface ICollectionRepository : IRepository<Collection, Guid>
{
    Task<IReadOnlyList<Collection>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Collection>> GetPublicForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Collection>> SearchForAdministrationAsync(string? query, int limit, CancellationToken cancellationToken = default);
}
