using GameCollector.Domain.Users;

namespace GameCollector.Application.Abstractions.Persistence;

public interface IUserProfileRepository : IRepository<UserProfile, Guid>
{
    Task<UserProfile?> GetBySubjectAsync(
        string identitySubject,
        CancellationToken cancellationToken = default);

    Task<bool> IsUsernameTakenAsync(
        string normalizedUsername,
        Guid? excludingUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> SearchAsync(
        string query,
        bool searchUsername,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> SearchForAdministrationAsync(
        string? query, int limit, CancellationToken cancellationToken = default);
}
