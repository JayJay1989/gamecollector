using System.Text.RegularExpressions;
using GameCollector.Domain.Common;

namespace GameCollector.Domain.Users;

public sealed partial class UserProfile
{
    private UserProfile()
    {
    }

    private UserProfile(
        Guid id,
        string identitySubject,
        string displayName,
        string username,
        DateTime createdAtUtc)
    {
        Id = id;
        IdentitySubject = ValidateIdentitySubject(identitySubject);
        SetProfile(displayName, username);
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public string IdentitySubject { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public bool IsDisabled { get; private set; }

    public Guid? DefaultCollectionId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static UserProfile Create(
        Guid id,
        string identitySubject,
        string displayName,
        string username,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("A user profile ID is required.");
        }

        return new UserProfile(id, identitySubject, displayName, username, createdAtUtc);
    }

    public void Update(string? displayName, string? username, DateTime updatedAtUtc)
    {
        if (displayName is null && username is null)
        {
            throw new DomainValidationException("At least one profile field must be supplied.");
        }

        SetProfile(displayName ?? DisplayName, username ?? Username);
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    public void Disable(DateTime updatedAtUtc)
    {
        IsDisabled = true;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    public void Enable(DateTime updatedAtUtc)
    {
        IsDisabled = false;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    public void SetDefaultCollection(Guid? collectionId, DateTime updatedAtUtc)
    {
        DefaultCollectionId = collectionId;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    private void SetProfile(string displayName, string username)
    {
        var trimmedDisplayName = displayName.Trim();
        var trimmedUsername = username.Trim();

        if (trimmedDisplayName.Length is < 1 or > 100)
        {
            throw new DomainValidationException("Display name must contain between 1 and 100 characters.");
        }

        if (!UsernamePattern().IsMatch(trimmedUsername))
        {
            throw new DomainValidationException(
                "Username must be 3-30 characters, begin with a letter or number, and contain only letters, numbers, dots, underscores, or hyphens.");
        }

        DisplayName = trimmedDisplayName;
        Username = trimmedUsername;
        NormalizedUsername = NormalizeUsername(trimmedUsername);
    }

    private static string ValidateIdentitySubject(string identitySubject)
    {
        var trimmedSubject = identitySubject.Trim();
        if (trimmedSubject.Length is < 1 or > 255)
        {
            throw new DomainValidationException("The identity subject is invalid.");
        }

        return trimmedSubject;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    [GeneratedRegex("^[\\p{L}\\p{N}][\\p{L}\\p{N}._-]{2,29}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
