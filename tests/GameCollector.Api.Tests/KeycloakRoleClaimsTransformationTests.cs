using System.Security.Claims;
using GameCollector.Api.Authentication;

namespace GameCollector.Api.Tests;

public sealed class KeycloakRoleClaimsTransformationTests
{
    private readonly KeycloakRoleClaimsTransformation _transformation = new();

    [Fact]
    public async Task RealmRolesAreAddedToAuthenticatedIdentity()
    {
        var identity = new ClaimsIdentity(
            [new Claim("realm_access", "{\"roles\":[\"gamecollector-admin\",\"offline_access\"]}")],
            "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var transformed = await _transformation.TransformAsync(principal);

        Assert.True(transformed.IsInRole("gamecollector-admin"));
        Assert.True(transformed.IsInRole("offline_access"));
    }

    [Fact]
    public async Task MalformedRealmAccessDoesNotGrantRoles()
    {
        var identity = new ClaimsIdentity(
            [new Claim("realm_access", "not-json")],
            "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var transformed = await _transformation.TransformAsync(principal);

        Assert.Empty(transformed.FindAll(ClaimTypes.Role));
    }
}
