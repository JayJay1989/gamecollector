using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Collections;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class CollectionRepository(ApplicationDbContext dbContext) : ICollectionRepository
{
    public Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Collections.Include(collection => collection.Members).SingleOrDefaultAsync(collection => collection.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Collection>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Collections.Include(collection => collection.Members)
            .Where(collection => collection.OwnerUserId == userId || collection.Members.Any(member => member.UserId == userId))
            .OrderBy(collection => collection.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Collection>> GetPublicForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
        await dbContext.Collections.AsNoTracking().Where(item => item.OwnerUserId == ownerUserId && item.IsPublic)
            .OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Collection>> SearchForAdministrationAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        var source = dbContext.Collections.Include(collection => collection.Members).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            source = source.Where(collection => EF.Functions.Like(collection.Name, pattern));
        }
        return await source.OrderBy(collection => collection.Name).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Collection entity, CancellationToken cancellationToken = default) => await dbContext.Collections.AddAsync(entity, cancellationToken);
    public void Remove(Collection entity) => dbContext.Collections.Remove(entity);
}
