namespace GameCollector.Contracts.Api;

public static class ModerationErrorCodes
{
    public const string SubmissionNotFound = "submission_not_found";
    public const string SubmissionNotEditable = "submission_not_editable";
    public const string SubmissionNotPending = "submission_not_pending";
    public const string RequiredImagesMissing = "required_images_missing";
    public const string InvalidReferenceData = "invalid_reference_data";
    public const string RevisionConflict = "catalog_revision_conflict";
    public const string ChangeRequestNotFound = "change_request_not_found";
    public const string ChangeRequestAlreadyPending = "change_request_already_pending";
    public const string ChangeRequestNotPending = "change_request_not_pending";
    public const string EmptyChangeRequest = "empty_change_request";
}
