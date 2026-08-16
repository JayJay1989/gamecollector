using GameCollector.Application.Abstractions.Authentication;
using GameCollector.Application.Abstractions.Persistence;
using GameCollector.Application.Common;
using GameCollector.Contracts.Users;
using GameCollector.Domain.Common;
using GameCollector.Domain.Users;
using GameCollector.Application.Notifications;
using GameCollector.Contracts.Notifications;

namespace GameCollector.Application.Users;

public sealed class DeviceService(
    ICurrentUser currentUser,
    IUserProfileRepository userProfiles,
    IDeviceRegistrationRepository devices,
    INotificationWriter notificationWriter,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IDeviceService
{
    public async Task<Result<DeviceRegistrationDto>> ActivateAsync(
        ActivateDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null)
        {
            return Result.Failure<DeviceRegistrationDto>(ApplicationErrors.ProfileNotFound);
        }

        if (profile.IsDisabled)
        {
            return Result.Failure<DeviceRegistrationDto>(ApplicationErrors.UserDisabled);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var registrationById = await devices.GetByIdAsync(request.DeviceId, cancellationToken);
        var registrationByUser = await devices.GetByUserIdAsync(profile.Id, cancellationToken);

        if (registrationById is not null && registrationById.UserId == profile.Id)
        {
            try
            {
                registrationById.Reactivate(request.FcmToken, now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success(Map(registrationById));
            }
            catch (DomainValidationException exception)
            {
                return Result.Failure<DeviceRegistrationDto>(ApplicationErrors.Validation(exception.Message));
            }
        }

        if (registrationByUser is not null && registrationByUser.DeviceId != request.DeviceId)
        {
            devices.Remove(registrationByUser);
        }

        if (registrationById is not null)
        {
            try
            {
                registrationById.ActivateForUser(profile.Id, request.FcmToken, now);
                if (registrationByUser is not null)
                    await notificationWriter.CreateAsync(profile.Id, NotificationTypes.DeviceRegistrationReplaced,
                        new { DeviceId = request.DeviceId }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success(Map(registrationById));
            }
            catch (DomainValidationException exception)
            {
                return Result.Failure<DeviceRegistrationDto>(ApplicationErrors.Validation(exception.Message));
            }
        }

        DeviceRegistration registration;
        try
        {
            registration = DeviceRegistration.Activate(
                request.DeviceId,
                profile.Id,
                request.FcmToken,
                now);
        }
        catch (DomainValidationException exception)
        {
            return Result.Failure<DeviceRegistrationDto>(ApplicationErrors.Validation(exception.Message));
        }

        await devices.AddAsync(registration, cancellationToken);
        if (registrationByUser is not null)
            await notificationWriter.CreateAsync(profile.Id, NotificationTypes.DeviceRegistrationReplaced,
                new { DeviceId = request.DeviceId }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(Map(registration));
    }

    public async Task<Result<bool>> RevokeAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);
        if (profile is null)
        {
            return Result.Failure<bool>(ApplicationErrors.ProfileNotFound);
        }

        if (profile.IsDisabled)
        {
            return Result.Failure<bool>(ApplicationErrors.UserDisabled);
        }

        var registration = await devices.GetByUserIdAsync(profile.Id, cancellationToken);
        if (registration is not null)
        {
            await notificationWriter.CreateAsync(profile.Id, NotificationTypes.DeviceRegistrationRevoked,
                new { registration.DeviceId }, cancellationToken);
            devices.Remove(registration);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(true);
    }

    public async Task<bool> IsActiveAsync(
        string identitySubject,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var profile = await userProfiles.GetBySubjectAsync(identitySubject, cancellationToken);
        if (profile is null || profile.IsDisabled)
        {
            return false;
        }

        var registration = await devices.GetByUserIdAsync(profile.Id, cancellationToken);
        return registration?.DeviceId == deviceId;
    }

    private Task<UserProfile?> GetProfileAsync(CancellationToken cancellationToken)
    {
        var subject = currentUser.Subject
            ?? throw new InvalidOperationException("The authenticated token has no subject claim.");
        return userProfiles.GetBySubjectAsync(subject, cancellationToken);
    }

    private static DeviceRegistrationDto Map(DeviceRegistration registration) => new(
        registration.DeviceId,
        registration.ActivatedAtUtc,
        registration.LastSeenAtUtc);
}
