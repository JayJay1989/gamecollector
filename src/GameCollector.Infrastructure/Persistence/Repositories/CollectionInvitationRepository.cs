using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Collections;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class CollectionInvitationRepository(ApplicationDbContext dbContext) : ICollectionInvitationRepository
{
    public Task<CollectionInvitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.CollectionInvitations.Include(invitation => invitation.Collection).ThenInclude(collection => collection.Members)
            .SingleOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);
    public async Task<IReadOnlyList<CollectionInvitation>> GetForInviteeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.CollectionInvitations.Include(invitation => invitation.Collection)
            .Where(invitation => invitation.InviteeUserId == userId && invitation.Status == InvitationStatus.Pending)
            .OrderByDescending(invitation => invitation.CreatedAtUtc).ToListAsync(cancellationToken);
    public Task<bool> HasPendingAsync(Guid collectionId, Guid inviteeUserId, CancellationToken cancellationToken = default) =>
        dbContext.CollectionInvitations.AnyAsync(invitation => invitation.CollectionId == collectionId && invitation.InviteeUserId == inviteeUserId && invitation.Status == InvitationStatus.Pending, cancellationToken);
    public async Task AddAsync(CollectionInvitation entity, CancellationToken cancellationToken = default) => await dbContext.CollectionInvitations.AddAsync(entity, cancellationToken);
    public void Remove(CollectionInvitation entity) => dbContext.CollectionInvitations.Remove(entity);
}
