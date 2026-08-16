using System.Security.Claims;
using System.Text.Json;
using GameCollector.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GameCollector.Api.Authentication;

public sealed class KeycloakRoleClaimsTransformation(IOptions<KeycloakOptions> options) : IClaimsTransformation
{
    private const string RealmAccessClaim = "realm_access";
    private const string ResourceAccessClaim = "resource_access";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        AddRealmRoles(principal, identity);
        AddAudienceClientRoles(principal, identity);

        return Task.FromResult(principal);
    }

    private static void AddRealmRoles(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        var value = principal.FindFirst(RealmAccessClaim)?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;
        try
        {
            using var document = JsonDocument.Parse(value);
            AddRoles(document.RootElement, principal, identity);
        }
        catch (JsonException)
        {
            // A malformed role claim grants no roles. JWT validation still controls authentication.
        }
    }

    private void AddAudienceClientRoles(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        var value = principal.FindFirst(ResourceAccessClaim)?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.TryGetProperty(options.Value.Audience, out var clientAccess))
            {
                AddRoles(clientAccess, principal, identity);
            }
        }
        catch (JsonException)
        {
            // A malformed role claim grants no roles. JWT validation still controls authentication.
        }
    }

    private static void AddRoles(JsonElement access, ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        if (!access.TryGetProperty("roles", out var roles) || roles.ValueKind is not JsonValueKind.Array) return;
        foreach (var roleElement in roles.EnumerateArray())
        {
            var role = roleElement.GetString();
            if (!string.IsNullOrWhiteSpace(role) && !principal.IsInRole(role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }
    }
}
