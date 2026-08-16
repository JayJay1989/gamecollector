using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class UserProfileRepository(ApplicationDbContext dbContext) : IUserProfileRepository
{
    public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.UserProfiles.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<UserProfile?> GetBySubjectAsync(
        string identitySubject,
        CancellationToken cancellationToken = default) =>
        dbContext.UserProfiles.SingleOrDefaultAsync(
            user => user.IdentitySubject == identitySubject,
            cancellationToken);

    public Task<bool> IsUsernameTakenAsync(
        string normalizedUsername,
        Guid? excludingUserId = null,
        CancellationToken cancellationToken = default) =>
        dbContext.UserProfiles.AnyAsync(
            user => user.NormalizedUsername == normalizedUsername &&
                    (!excludingUserId.HasValue || user.Id != excludingUserId.Value),
            cancellationToken);

    public async Task AddAsync(UserProfile entity, CancellationToken cancellationToken = default) =>
        await dbContext.UserProfiles.AddAsync(entity, cancellationToken);

    public void Remove(UserProfile entity) => dbContext.UserProfiles.Remove(entity);

    public async Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await dbContext.UserProfiles.Where(user => ids.Contains(user.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> SearchAsync(string query, bool searchUsername, int limit, CancellationToken cancellationToken = default)
    {
        var normalized = query.Trim().ToUpperInvariant();
        var displayPattern = $"%{query.Trim()}%";
        var usernamePattern = $"%{normalized}%";
        return await dbContext.UserProfiles
            .Where(user => !user.IsDisabled && (searchUsername
                ? EF.Functions.Like(user.NormalizedUsername, usernamePattern)
                : EF.Functions.Like(user.DisplayName, displayPattern)))
            .OrderBy(user => user.Username)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserProfile>> SearchForAdministrationAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        var source = dbContext.UserProfiles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            source = source.Where(user => EF.Functions.Like(user.DisplayName, pattern) ||
                                          EF.Functions.Like(user.Username, pattern));
        }
        return await source.OrderBy(user => user.Username).Take(limit).ToListAsync(cancellationToken);
    }
}
