namespace GameCollector.Contracts.Api;

public static class CollectionErrorCodes
{
    public const string CollectionNotFound = "collection_not_found";
    public const string CollectionAccessDenied = "collection_access_denied";
    public const string CollectionOwnerRequired = "collection_owner_required";
    public const string MemberNotFound = "collection_member_not_found";
    public const string InvitationNotFound = "invitation_not_found";
    public const string InvitationAlreadyPending = "invitation_already_pending";
    public const string InvitationNotPending = "invitation_not_pending";
    public const string InvalidCollectionRole = "invalid_collection_role";
    public const string OwnerTransferRequired = "owner_transfer_required";
    public const string CollectionEditRequired = "collection_edit_required";
}
