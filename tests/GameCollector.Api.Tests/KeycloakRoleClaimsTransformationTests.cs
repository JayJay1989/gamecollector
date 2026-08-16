using System.Security.Claims;
using GameCollector.Api.Authentication;
using GameCollector.Api.Configuration;
using Microsoft.Extensions.Options;

namespace GameCollector.Api.Tests;

public sealed class KeycloakRoleClaimsTransformationTests
{
    private readonly KeycloakRoleClaimsTransformation _transformation = new(Options.Create(new KeycloakOptions
    {
        Authority = "https://sso.example.test/realms/test",
        Audience = "gamecollector-api",
        AdminRole = "gamecollector-admin"
    }));

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

    [Fact]
    public async Task ConfiguredAudienceClientRolesAreAddedAlongsideRealmRoles()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("realm_access", "{\"roles\":[\"offline_access\"]}"),
            new Claim("resource_access", "{\"gamecollector-api\":{\"roles\":[\"viewer\",\"gamecollector-admin\"]},\"another-client\":{\"roles\":[\"ignored-role\"]}}")
        ], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var transformed = await _transformation.TransformAsync(principal);

        Assert.True(transformed.IsInRole("offline_access"));
        Assert.True(transformed.IsInRole("viewer"));
        Assert.True(transformed.IsInRole("gamecollector-admin"));
        Assert.False(transformed.IsInRole("ignored-role"));
    }
}
