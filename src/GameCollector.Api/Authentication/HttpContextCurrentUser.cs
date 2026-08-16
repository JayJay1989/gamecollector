using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Api.Configuration;

namespace GameCollector.Api.Authentication;

public sealed class HttpContextCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    Microsoft.Extensions.Options.IOptions<KeycloakOptions> keycloakOptions) : ICurrentUser
{
    public string? Subject => httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
    public bool IsAdministrator => httpContextAccessor.HttpContext?.User.IsInRole(keycloakOptions.Value.AdminRole) is true;
}
