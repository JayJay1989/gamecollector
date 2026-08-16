namespace GameCollector.Infrastructure.Media;

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";
    public string Endpoint { get; init; } = "localhost:9000";
    public string AccessKey { get; init; } = "gamecollector";
    public string SecretKey { get; init; } = "change-me";
    public string Bucket { get; init; } = "gamecollector-media";
    public bool UseSsl { get; init; }
}
