using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GameCollector.Application.Abstractions.ExternalCatalog;

namespace GameCollector.Infrastructure.ExternalCatalog;

public sealed class UpcItemDbProductMetadataProvider(HttpClient httpClient) : IProductMetadataProvider
{
    public async Task<ProductMetadataCandidate?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"lookup?upc={Uri.EscapeDataString(barcode)}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) return null;
            var payload = await response.Content.ReadFromJsonAsync<LookupResponse>(cancellationToken);
            var item = payload?.Items is { Count: > 0 } items ? items[0] : null;
            return item is null ? null : new ProductMetadataCandidate("upcitemdb", Clean(item.Title), Clean(item.Brand), Clean(item.Description));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (HttpRequestException) { return null; }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record LookupResponse([property: JsonPropertyName("items")] IReadOnlyList<LookupItem>? Items);
    private sealed record LookupItem(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("brand")] string? Brand,
        [property: JsonPropertyName("description")] string? Description);
}
