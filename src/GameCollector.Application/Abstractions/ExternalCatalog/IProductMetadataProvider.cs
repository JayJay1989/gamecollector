namespace GameCollector.Application.Abstractions.ExternalCatalog;

public sealed record ProductMetadataCandidate(string Source, string? Title, string? Publisher, string? Description);

public interface IProductMetadataProvider
{
    Task<ProductMetadataCandidate?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken);
}
