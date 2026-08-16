using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence.Repositories;

public sealed class CatalogRepository(ApplicationDbContext dbContext) : ICatalogRepository
{
    public Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Detailed().SingleOrDefaultAsync(game => game.Id == id, cancellationToken);
    public async Task AddAsync(Game game, CancellationToken cancellationToken = default) => await dbContext.Games.AddAsync(game, cancellationToken);
    public void Remove(Game game) => dbContext.Games.Remove(game);
    public async Task<IReadOnlyList<Game>> GetSubmissionsForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await Detailed().Where(game => game.SubmittedByUserId == userId).OrderByDescending(game => game.UpdatedAtUtc).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Game>> GetSubmissionsForModerationAsync(ModerationStatus? status, CancellationToken cancellationToken = default)
    {
        var source = Detailed().Where(game => game.SubmittedByUserId != null && game.ModerationStatus != ModerationStatus.Draft);
        if (status.HasValue) source = source.Where(game => game.ModerationStatus == status.Value);
        return await source.OrderByDescending(game => game.UpdatedAtUtc).ToListAsync(cancellationToken);
    }
    public Task<Game?> GetVisibleByIdAsync(Guid id, Guid userId, bool isAdministrator, CancellationToken cancellationToken = default) =>
        Visible(userId, isAdministrator).SingleOrDefaultAsync(game => game.Id == id, cancellationToken);
    public Task<Game?> GetVisibleByBarcodeAsync(string barcode, Guid userId, bool isAdministrator, CancellationToken cancellationToken = default) =>
        Visible(userId, isAdministrator).SingleOrDefaultAsync(game => game.Barcodes.Any(item => item.NormalizedBarcode == barcode), cancellationToken);
    public async Task<IReadOnlyList<Game>> SearchVisibleAsync(string? query, Guid userId, bool isAdministrator, int limit, CancellationToken cancellationToken = default)
    {
        var source = Visible(userId, isAdministrator);
        if (!string.IsNullOrWhiteSpace(query)) { var pattern = $"%{query.Trim()}%"; source = source.Where(game => EF.Functions.Like(game.Title, pattern)); }
        return await source.OrderBy(game => game.Title).Take(limit).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<Game>> GetAllVisibleAsync(Guid userId, bool isAdministrator, CancellationToken cancellationToken = default) =>
        await Visible(userId, isAdministrator).OrderBy(game => game.Title).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Language>> GetLanguagesAsync(CancellationToken cancellationToken = default) => await dbContext.Languages.OrderBy(item => item.Name).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default) => await dbContext.Tags.OrderBy(item => item.Name).ToListAsync(cancellationToken);

    private IQueryable<Game> Visible(Guid userId, bool administrator) => Detailed()
        .Where(game => administrator || game.ModerationStatus == ModerationStatus.Approved || game.SubmittedByUserId == userId ||
            ((game.ModerationStatus == ModerationStatus.Pending || game.ModerationStatus == ModerationStatus.NeedsChanges) &&
             dbContext.CollectionGames.Any(owned => owned.GameId == game.Id && owned.IsOwned &&
                (owned.Collection.OwnerUserId == userId || owned.Collection.Members.Any(member => member.UserId == userId)))));

    private IQueryable<Game> Detailed() => dbContext.Games
        .Include(game => game.Barcodes).Include(game => game.Languages).ThenInclude(item => item.Language)
        .Include(game => game.Tags).ThenInclude(item => item.Tag);
}
