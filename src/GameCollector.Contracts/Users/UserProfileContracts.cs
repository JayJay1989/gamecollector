using System.ComponentModel.DataAnnotations;

namespace GameCollector.Contracts.Users;

public sealed record OnboardUserRequest(
    [Required, StringLength(100, MinimumLength = 1)] string DisplayName,
    [Required, StringLength(30, MinimumLength = 3)] string Username);

public sealed record UpdateUserProfileRequest(
    [StringLength(100, MinimumLength = 1)] string? DisplayName,
    [StringLength(30, MinimumLength = 3)] string? Username);

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    string Username,
    bool HasActiveDevice,
    Guid? DefaultCollectionId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
