using System.Net;
using System.Text;
using GameCollector.Infrastructure.ExternalCatalog;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GameCollector.Infrastructure.Tests;

public sealed class ExternalProductCacheTests
{
    [Fact]
    public async Task RepeatedBarcodeLookupUsesCachedProviderResult()
    {
        var handler = new CountingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://provider.test/") };
        var inner = new UpcItemDbProductMetadataProvider(httpClient);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CachedProductMetadataProvider(inner, cache,
            Options.Create(new ExternalCatalogOptions { CacheMinutes = 10 }));

        var first = await provider.LookupBarcodeAsync("887961751062", CancellationToken.None);
        var second = await provider.LookupBarcodeAsync("887961751062", CancellationToken.None);

        Assert.Equal("Test Game", first?.Title);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            const string payload = "{\"items\":[{\"title\":\"Test Game\",\"brand\":\"Test Publisher\",\"description\":\"Description\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
