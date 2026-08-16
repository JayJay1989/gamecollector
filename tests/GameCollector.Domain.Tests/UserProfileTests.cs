using GameCollector.Domain.Common;
using GameCollector.Domain.Users;

namespace GameCollector.Domain.Tests;

public sealed class UserProfileTests
{
    [Fact]
    public void CreateTrimsAndNormalizesUsername()
    {
        var profile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-subject",
            "  John Smith  ",
            "  John_1  ",
            DateTime.UtcNow);

        Assert.Equal("John Smith", profile.DisplayName);
        Assert.Equal("John_1", profile.Username);
        Assert.Equal("JOHN_1", profile.NormalizedUsername);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("#john")]
    [InlineData("john space")]
    public void CreateRejectsInvalidUsername(string username)
    {
        Assert.Throws<DomainValidationException>(() => UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-subject",
            "John Smith",
            username,
            DateTime.UtcNow));
    }

    [Fact]
    public void DisableChangesApplicationAccessState()
    {
        var profile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-subject",
            "John Smith",
            "john",
            DateTime.UtcNow);

        profile.Disable(DateTime.UtcNow.AddMinutes(1));

        Assert.True(profile.IsDisabled);
    }
}
