using GameCollector.Domain.Catalog;

namespace GameCollector.Application.Abstractions.Persistence;

public interface ICatalogRepository
{
    Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Game game, CancellationToken cancellationToken = default);
    void Remove(Game game);
    Task<IReadOnlyList<Game>> GetSubmissionsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Game>> GetSubmissionsForModerationAsync(ModerationStatus? status, CancellationToken cancellationToken = default);
    Task<Game?> GetVisibleByIdAsync(Guid id, Guid userId, bool isAdministrator, CancellationToken cancellationToken = default);
    Task<Game?> GetVisibleByBarcodeAsync(string barcode, Guid userId, bool isAdministrator, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Game>> SearchVisibleAsync(string? query, Guid userId, bool isAdministrator, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Game>> GetAllVisibleAsync(Guid userId, bool isAdministrator, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Language>> GetLanguagesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default);
}
