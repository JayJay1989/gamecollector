namespace GameCollector.Contracts.Api;

public static class MediaErrorCodes
{
    public const string MediaNotFound = "media_not_found";
    public const string MediaAccessDenied = "media_access_denied";
    public const string MediaAlreadyExists = "media_already_exists";
    public const string InvalidMediaRequest = "invalid_media_request";
    public const string UploadNotFound = "media_upload_not_found";
    public const string UploadNotPending = "media_upload_not_pending";
    public const string InvalidImage = "invalid_image";
}
