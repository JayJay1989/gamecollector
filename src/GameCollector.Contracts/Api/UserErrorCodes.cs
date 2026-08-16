namespace GameCollector.Contracts.Api;

public static class UserErrorCodes
{
    public const string ProfileNotFound = "profile_not_found";
    public const string ProfileAlreadyExists = "profile_already_exists";
    public const string UsernameAlreadyExists = "username_already_exists";
    public const string UserDisabled = "user_disabled";
    public const string DeviceNotActive = "device_not_active";
}
