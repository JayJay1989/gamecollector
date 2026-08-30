using System.Text.Json;

namespace GameCollector.Contracts.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Type,
    JsonElement Payload,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public static class NotificationTypes
{
    public const string CollectionInvitation = "CollectionInvitation";
    public const string InvitationAccepted = "InvitationAccepted";
    public const string InvitationDeclined = "InvitationDeclined";
    public const string CollectionMembershipChanged = "CollectionMembershipChanged";
    public const string CollectionMembershipRemoved = "CollectionMembershipRemoved";
    public const string GameSubmissionApproved = "GameSubmissionApproved";
    public const string GameSubmissionNeedsChanges = "GameSubmissionNeedsChanges";
    public const string GameSubmissionRejected = "GameSubmissionRejected";
    public const string SuggestedEditApproved = "SuggestedEditApproved";
    public const string SuggestedEditRejected = "SuggestedEditRejected";
    public const string DeviceRegistrationReplaced = "DeviceRegistrationReplaced";
    public const string DeviceRegistrationRevoked = "DeviceRegistrationRevoked";
    public const string FriendRequest = "FriendRequest";
    public const string FriendRequestAccepted = "FriendRequestAccepted";
    public const string FriendRequestDeclined = "FriendRequestDeclined";
}
