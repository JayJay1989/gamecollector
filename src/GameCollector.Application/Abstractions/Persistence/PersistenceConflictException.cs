namespace GameCollector.Application.Abstractions.Persistence;

public sealed class PersistenceConflictException(string constraint, Exception innerException)
    : Exception("A unique persistence constraint was violated.", innerException)
{
    public string Constraint { get; } = constraint;
}

public static class PersistenceConstraints
{
    public const string IdentitySubject = "user_identity_subject";
    public const string NormalizedUsername = "user_normalized_username";
    public const string ActiveDeviceUser = "active_device_user";
    public const string ActiveDeviceId = "active_device_id";
    public const string CollectionGame = "collection_game";
    public const string WishlistItem = "wishlist_item";
    public const string GameBarcode = "game_barcode";
    public const string GameImageType = "game_image_type";
    public const string PendingGameChangeRequest = "pending_game_change_request";
}

public sealed class PersistenceConcurrencyException(Exception innerException)
    : Exception("The persisted entity was changed by another request.", innerException);
