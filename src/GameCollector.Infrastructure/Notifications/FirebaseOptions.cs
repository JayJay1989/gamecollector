namespace GameCollector.Infrastructure.Notifications;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";
    public string ProjectId { get; set; } = string.Empty;
    public string? CredentialsPath { get; set; }
}
