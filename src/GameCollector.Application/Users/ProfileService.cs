using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Common;
using GameCollector.Domain.Users;
using GameCollector.Application.Sync;

namespace GameCollector.Application.Users;

public sealed class ProfileService(
    ICurrentUser currentUser,
    IUserProfileRepository userProfiles,
    IDeviceRegistrationRepository devices,
    IUnitOfWork unitOfWork,
    ISyncEventWriter syncEvents,
    TimeProvider timeProvider) : IProfileService
{
    public async Task<Result<UserProfileDto>> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.ProfileNotFound);
        }

        if (profile.IsDisabled)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.UserDisabled);
        }

        return Result.Success(await MapAsync(profile, cancellationToken));
    }

    public async Task<Result<UserProfileDto>> OnboardAsync(
        OnboardUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var subject = GetRequiredSubject();
        var existingProfile = await userProfiles.GetBySubjectAsync(subject, cancellationToken);
        if (existingProfile is not null)
        {
            return Result.Failure<UserProfileDto>(
                existingProfile.IsDisabled
                    ? ApplicationErrors.UserDisabled
                    : ApplicationErrors.ProfileAlreadyExists);
        }

        string normalizedUsername;
        try
        {
            normalizedUsername = UserProfile.NormalizeUsername(request.Username);
        }
        catch (NullReferenceException)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.Validation("Username is required."));
        }

        if (await userProfiles.IsUsernameTakenAsync(normalizedUsername, cancellationToken: cancellationToken))
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.UsernameAlreadyExists);
        }

        UserProfile profile;
        try
        {
            profile = UserProfile.Create(
                Guid.NewGuid(),
                subject,
                request.DisplayName,
                request.Username,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (DomainValidationException exception)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.Validation(exception.Message));
        }

        await userProfiles.AddAsync(profile, cancellationToken);
        await syncEvents.WriteAsync("user", profile.Id, "profileChanged", profile.Id,
            new { profile.Id, profile.DisplayName, profile.Username, profile.DefaultCollectionId, profile.UpdatedAtUtc }, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConflictException exception)
        {
            return Result.Failure<UserProfileDto>(exception.Constraint switch
            {
                PersistenceConstraints.IdentitySubject => ApplicationErrors.ProfileAlreadyExists,
                _ => ApplicationErrors.UsernameAlreadyExists
            });
        }

        return Result.Success(await MapAsync(profile, cancellationToken));
    }

    public async Task<Result<UserProfileDto>> UpdateAsync(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.ProfileNotFound);
        }

        if (profile.IsDisabled)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.UserDisabled);
        }

        if (request.Username is not null)
        {
            var normalizedUsername = UserProfile.NormalizeUsername(request.Username);
            if (!string.Equals(normalizedUsername, profile.NormalizedUsername, StringComparison.Ordinal) &&
                await userProfiles.IsUsernameTakenAsync(
                    normalizedUsername,
                    profile.Id,
                    cancellationToken))
            {
                return Result.Failure<UserProfileDto>(ApplicationErrors.UsernameAlreadyExists);
            }
        }

        try
        {
            profile.Update(
                request.DisplayName,
                request.Username,
                timeProvider.GetUtcNow().UtcDateTime);
            await syncEvents.WriteAsync("user", profile.Id, "profileChanged", profile.Id,
                new { profile.Id, profile.DisplayName, profile.Username, profile.DefaultCollectionId, profile.UpdatedAtUtc }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.Validation(exception.Message));
        }
        catch (PersistenceConflictException)
        {
            return Result.Failure<UserProfileDto>(ApplicationErrors.UsernameAlreadyExists);
        }

        return Result.Success(await MapAsync(profile, cancellationToken));
    }

    private Task<UserProfile?> GetCurrentProfileAsync(CancellationToken cancellationToken) =>
        userProfiles.GetBySubjectAsync(GetRequiredSubject(), cancellationToken);

    private string GetRequiredSubject() => currentUser.Subject
        ?? throw new InvalidOperationException("The authenticated token has no subject claim.");

    private async Task<UserProfileDto> MapAsync(
        UserProfile profile,
        CancellationToken cancellationToken)
    {
        var device = await devices.GetByUserIdAsync(profile.Id, cancellationToken);
        return new UserProfileDto(
            profile.Id,
            profile.DisplayName,
            profile.Username,
            device is not null,
            profile.DefaultCollectionId,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
    }
}
