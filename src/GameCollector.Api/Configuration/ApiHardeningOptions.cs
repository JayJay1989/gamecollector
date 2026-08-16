namespace GameCollector.Api.Configuration;

public sealed class ApiHardeningOptions
{
    public const string SectionName = "ApiHardening";
    public long MaximumRequestBodyBytes { get; init; } = 1_048_576;
    public int RateLimitPermitCount { get; init; } = 120;
    public int RateLimitWindowSeconds { get; init; } = 60;
    public int RequestTimeoutSeconds { get; init; } = 30;
}
