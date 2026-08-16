using System.ComponentModel.DataAnnotations;

namespace GameCollector.Api.Configuration;

public sealed class KeycloakOptions
{
    public const string SectionName = "Authentication:Keycloak";

    [Required]
    [Url]
    public required string Authority { get; init; }

    [Required]
    public required string Audience { get; init; }

    [Required]
    public required string AdminRole { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;
}
