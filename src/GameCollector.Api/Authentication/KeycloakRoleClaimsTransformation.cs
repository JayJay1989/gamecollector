using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace GameCollector.Api.Authentication;

public sealed class KeycloakRoleClaimsTransformation : IClaimsTransformation
{
    private const string RealmAccessClaim = "realm_access";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var realmAccess = principal.FindFirst(RealmAccessClaim)?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return Task.FromResult(principal);
        }

        try
        {
            using var document = JsonDocument.Parse(realmAccess);
            if (!document.RootElement.TryGetProperty("roles", out var roles) ||
                roles.ValueKind is not JsonValueKind.Array)
            {
                return Task.FromResult(principal);
            }

            foreach (var roleElement in roles.EnumerateArray())
            {
                var role = roleElement.GetString();
                if (!string.IsNullOrWhiteSpace(role) && !principal.IsInRole(role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }
        }
        catch (JsonException)
        {
            // A malformed role claim grants no roles. JWT validation still controls authentication.
        }

        return Task.FromResult(principal);
    }
}
