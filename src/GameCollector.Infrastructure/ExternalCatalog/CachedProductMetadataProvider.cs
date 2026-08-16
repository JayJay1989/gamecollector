using GameCollector.Application.Abstractions.ExternalCatalog;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GameCollector.Infrastructure.ExternalCatalog;

public sealed class CachedProductMetadataProvider(UpcItemDbProductMetadataProvider inner,
    IMemoryCache cache, IOptions<ExternalCatalogOptions> options) : IProductMetadataProvider
{
    private static readonly object Missing = new();
    private readonly TimeSpan _lifetime = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));

    public async Task<ProductMetadataCandidate?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var key = $"external-product:{barcode}";
        if (cache.TryGetValue(key, out object? cached))
            return ReferenceEquals(cached, Missing) ? null : (ProductMetadataCandidate?)cached;
        var candidate = await inner.LookupBarcodeAsync(barcode, cancellationToken);
        cache.Set(key, candidate ?? Missing, _lifetime);
        return candidate;
    }
}
