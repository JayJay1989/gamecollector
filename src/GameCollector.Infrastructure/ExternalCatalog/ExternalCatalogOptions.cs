namespace GameCollector.Infrastructure.ExternalCatalog;

public sealed class ExternalCatalogOptions
{
    public const string SectionName = "ExternalCatalog";
    public string BaseUrl { get; init; } = "https://api.upcitemdb.com/prod/trial/";
    public int TimeoutSeconds { get; init; } = 5;
    public int CacheMinutes { get; init; } = 60;
}
