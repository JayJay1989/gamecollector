using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Catalog;
using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;
using GameCollector.Domain.Users;

namespace GameCollector.Application.Catalog;

public sealed class CatalogService(ICurrentUser currentUser, IUserProfileRepository users, ICatalogRepository catalog) : ICatalogService
{
    public async Task<Result<IReadOnlyList<GameSummaryDto>>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<GameSummaryDto>>(ApplicationErrors.ProfileNotFound);
        var games = await catalog.SearchVisibleAsync(query, profile.Id, currentUser.IsAdministrator, 50, cancellationToken);
        return Result.Success<IReadOnlyList<GameSummaryDto>>(games.Select(game => new GameSummaryDto(game.Id, game.Title, game.Publisher, game.ReleaseYear, game.ModerationStatus.ToString())).ToList());
    }

    public async Task<Result<GameDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<GameDto>(ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetVisibleByIdAsync(id, profile.Id, currentUser.IsAdministrator, cancellationToken);
        return game is null ? Result.Failure<GameDto>(ApplicationErrors.GameNotFound) : Result.Success(Map(game));
    }

    public async Task<Result<GameDto>> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        string normalized;
        try { normalized = GameBarcode.NormalizeAndValidate(barcode); }
        catch (DomainValidationException) { return Result.Failure<GameDto>(ApplicationErrors.InvalidBarcode); }
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null) return Result.Failure<GameDto>(ApplicationErrors.ProfileNotFound);
        var game = await catalog.GetVisibleByBarcodeAsync(normalized, profile.Id, currentUser.IsAdministrator, cancellationToken);
        return game is null ? Result.Failure<GameDto>(ApplicationErrors.BarcodeNotFound) : Result.Success(Map(game));
    }

    public async Task<Result<IReadOnlyList<ReferenceDataDto>>> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var items = await catalog.GetLanguagesAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ReferenceDataDto>>(items.Select(item => new ReferenceDataDto(item.Id, item.Name, item.Code)).ToList());
    }

    public async Task<Result<IReadOnlyList<ReferenceDataDto>>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        var items = await catalog.GetTagsAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ReferenceDataDto>>(items.Select(item => new ReferenceDataDto(item.Id, item.Name)).ToList());
    }

    private Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken) => users.GetBySubjectAsync(currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
    private static GameDto Map(Game game) => new(game.Id, game.Title, game.Description, game.Publisher, game.ReleaseYear,
        game.MinimumPlayers, game.MaximumPlayers, game.MinimumAge, game.MinimumPlayingTimeMinutes,
        game.MaximumPlayingTimeMinutes, game.ModerationStatus.ToString(), game.Revision,
        game.Barcodes.Select(item => item.NormalizedBarcode).ToList(),
        game.Languages.Select(item => new ReferenceDataDto(item.Language.Id, item.Language.Name, item.Language.Code)).OrderBy(item => item.Name).ToList(),
        game.Tags.Select(item => new ReferenceDataDto(item.Tag.Id, item.Tag.Name)).OrderBy(item => item.Name).ToList());
}
