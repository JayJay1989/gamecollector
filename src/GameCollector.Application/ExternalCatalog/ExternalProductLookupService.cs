using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.ExternalCatalog;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Media;
using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;

namespace GameCollector.Application.ExternalCatalog;

public sealed class ExternalProductLookupService(ICurrentUser currentUser, IUserProfileRepository users,
    ICatalogRepository catalog, IProductMetadataProvider provider) : IExternalProductLookupService
{
    public async Task<Result<ProductMetadataCandidateDto>> LookupAsync(string barcode, CancellationToken cancellationToken = default)
    {
        string normalized;
        try { normalized = GameBarcode.NormalizeAndValidate(barcode); }
        catch (DomainValidationException) { return Result.Failure<ProductMetadataCandidateDto>(ApplicationErrors.InvalidBarcode); }
        var profile = await users.GetBySubjectAsync(currentUser.Subject ?? throw new InvalidOperationException("Missing subject claim."), cancellationToken);
        if (profile is null) return Result.Failure<ProductMetadataCandidateDto>(ApplicationErrors.ProfileNotFound);
        var existing = await catalog.GetVisibleByBarcodeAsync(normalized, profile.Id, currentUser.IsAdministrator, cancellationToken);
        if (existing is not null)
            return Result.Success(new ProductMetadataCandidateDto(normalized, "catalog", existing.Id, existing.Title, existing.Publisher, existing.Description));
        var candidate = await provider.LookupBarcodeAsync(normalized, cancellationToken);
        return candidate is null
            ? Result.Failure<ProductMetadataCandidateDto>(ApplicationErrors.BarcodeNotFound)
            : Result.Success(new ProductMetadataCandidateDto(normalized, candidate.Source, null, candidate.Title, candidate.Publisher, candidate.Description));
    }
}
