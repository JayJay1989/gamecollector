namespace GameCollector.Contracts.Api;

public static class CatalogErrorCodes
{
    public const string GameNotFound = "game_not_found";
    public const string BarcodeNotFound = "barcode_not_found";
    public const string BarcodeAlreadyExists = "barcode_already_exists";
    public const string InvalidBarcode = "invalid_barcode";
    public const string GameAlreadyOwned = "game_already_owned";
}
