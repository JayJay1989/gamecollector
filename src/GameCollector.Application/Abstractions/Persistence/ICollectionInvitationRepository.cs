using GameCollector.Domain.Collections;

namespace GameCollector.Application.Abstractions.Persistence;

public interface ICollectionInvitationRepository : IRepository<CollectionInvitation, Guid>
{
    Task<IReadOnlyList<CollectionInvitation>> GetForInviteeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasPendingAsync(Guid collectionId, Guid inviteeUserId, CancellationToken cancellationToken = default);
}
