using GameCollector.Contracts.Api;

namespace GameCollector.Application.Common;

public static class ApplicationErrors
{
    public static readonly ApplicationError ProfileNotFound = new(
        UserErrorCodes.ProfileNotFound,
        "The user profile does not exist.",
        ApplicationErrorType.NotFound);

    public static readonly ApplicationError ProfileAlreadyExists = new(
        UserErrorCodes.ProfileAlreadyExists,
        "The user profile already exists.",
        ApplicationErrorType.Conflict);

    public static readonly ApplicationError UsernameAlreadyExists = new(
        UserErrorCodes.UsernameAlreadyExists,
        "The username is already in use.",
        ApplicationErrorType.Conflict);

    public static readonly ApplicationError UserDisabled = new(
        UserErrorCodes.UserDisabled,
        "Application access is disabled for this user.",
        ApplicationErrorType.Forbidden);

    public static ApplicationError Validation(string title) => new(
        ApiErrorCodes.DomainValidationFailed,
        title,
        ApplicationErrorType.Validation);

    public static readonly ApplicationError CollectionNotFound = new(CollectionErrorCodes.CollectionNotFound, "The collection does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError CollectionAccessDenied = new(CollectionErrorCodes.CollectionAccessDenied, "You cannot access this collection.", ApplicationErrorType.Forbidden);
    public static readonly ApplicationError CollectionOwnerRequired = new(CollectionErrorCodes.CollectionOwnerRequired, "Only the collection owner can perform this action.", ApplicationErrorType.Forbidden);
    public static readonly ApplicationError MemberNotFound = new(CollectionErrorCodes.MemberNotFound, "The collection member does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError InvitationNotFound = new(CollectionErrorCodes.InvitationNotFound, "The invitation does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError InvitationAlreadyPending = new(CollectionErrorCodes.InvitationAlreadyPending, "An invitation is already pending.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError InvitationNotPending = new(CollectionErrorCodes.InvitationNotPending, "The invitation is no longer pending.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError InvalidCollectionRole = new(CollectionErrorCodes.InvalidCollectionRole, "The collection role is invalid.", ApplicationErrorType.Validation);
    public static readonly ApplicationError OwnerTransferRequired = new(CollectionErrorCodes.OwnerTransferRequired, "Transfer ownership before the owner can leave.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError GameNotFound = new(CatalogErrorCodes.GameNotFound, "The game does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError BarcodeNotFound = new(CatalogErrorCodes.BarcodeNotFound, "No visible game has this barcode.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError InvalidBarcode = new(CatalogErrorCodes.InvalidBarcode, "The barcode is invalid.", ApplicationErrorType.Validation);
    public static readonly ApplicationError CollectionEditRequired = new(CollectionErrorCodes.CollectionEditRequired, "Owner or Editor access is required.", ApplicationErrorType.Forbidden);
    public static readonly ApplicationError MediaNotFound = new(MediaErrorCodes.MediaNotFound, "The media item does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError MediaAccessDenied = new(MediaErrorCodes.MediaAccessDenied, "You cannot change this game's media.", ApplicationErrorType.Forbidden);
    public static readonly ApplicationError MediaAlreadyExists = new(MediaErrorCodes.MediaAlreadyExists, "This game already has an image of that type.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError InvalidMediaRequest = new(MediaErrorCodes.InvalidMediaRequest, "The image type, content type, or file size is invalid.", ApplicationErrorType.Validation);
    public static readonly ApplicationError UploadNotFound = new(MediaErrorCodes.UploadNotFound, "The uploaded object was not found.", ApplicationErrorType.Validation);
    public static readonly ApplicationError UploadNotPending = new(MediaErrorCodes.UploadNotPending, "The image upload is no longer pending.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError InvalidImage = new(MediaErrorCodes.InvalidImage, "The uploaded object is not an accepted image.", ApplicationErrorType.Validation);
    public static readonly ApplicationError SubmissionNotFound = new(ModerationErrorCodes.SubmissionNotFound, "The game submission does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError SubmissionNotEditable = new(ModerationErrorCodes.SubmissionNotEditable, "The game submission is not editable.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError SubmissionNotPending = new(ModerationErrorCodes.SubmissionNotPending, "The game submission is not pending review.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError RequiredImagesMissing = new(ModerationErrorCodes.RequiredImagesMissing, "A ready front image is required.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError InvalidReferenceData = new(ModerationErrorCodes.InvalidReferenceData, "One or more language or tag IDs are invalid.", ApplicationErrorType.Validation);
    public static readonly ApplicationError RevisionConflict = new(ModerationErrorCodes.RevisionConflict, "The catalog record changed; reload it before trying again.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError ChangeRequestNotFound = new(ModerationErrorCodes.ChangeRequestNotFound, "The game change request does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError ChangeRequestAlreadyPending = new(ModerationErrorCodes.ChangeRequestAlreadyPending, "You already have a pending change request for this game.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError ChangeRequestNotPending = new(ModerationErrorCodes.ChangeRequestNotPending, "The game change request has already been reviewed.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError EmptyChangeRequest = new(ModerationErrorCodes.EmptyChangeRequest, "At least one proposed change is required.", ApplicationErrorType.Validation);
    public static readonly ApplicationError InvalidSyncRequest = new(SyncErrorCodes.InvalidSyncRequest, "The synchronization request is invalid.", ApplicationErrorType.Validation);
    public static readonly ApplicationError InvalidSyncScope = new(SyncErrorCodes.InvalidSyncScope, "The synchronization scope is invalid.", ApplicationErrorType.Validation);
    public static readonly ApplicationError SyncScopeAccessDenied = new(SyncErrorCodes.SyncScopeAccessDenied, "You cannot synchronize this scope.", ApplicationErrorType.Forbidden);
    public static readonly ApplicationError SyncResetRequired = new(SyncErrorCodes.SyncResetRequired, "The cursor is older than retained history; reset this scope.", ApplicationErrorType.Conflict);
    public static readonly ApplicationError NotificationNotFound = new(NotificationErrorCodes.NotificationNotFound, "The notification does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError AdminUserNotFound = new(AdminErrorCodes.AdminUserNotFound, "The user does not exist.", ApplicationErrorType.NotFound);
    public static readonly ApplicationError AdminCannotDisableSelf = new(AdminErrorCodes.AdminCannotDisableSelf, "Administrators cannot disable their own application profile.", ApplicationErrorType.Conflict);
}
