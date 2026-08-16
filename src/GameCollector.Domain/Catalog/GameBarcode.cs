using GameCollector.Domain.Common;

namespace GameCollector.Domain.Catalog;

public sealed class GameBarcode
{
    private GameBarcode() { }
    private GameBarcode(Guid id, Guid gameId, string barcode) { Id = id; GameId = gameId; Barcode = barcode; NormalizedBarcode = NormalizeAndValidate(barcode); }
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string Barcode { get; private set; } = string.Empty;
    public string NormalizedBarcode { get; private set; } = string.Empty;
    public Game Game { get; private set; } = null!;
    public static GameBarcode Create(Guid id, Guid gameId, string barcode)
    {
        if (id == Guid.Empty || gameId == Guid.Empty) throw new DomainValidationException("Valid barcode IDs are required.");
        return new GameBarcode(id, gameId, barcode.Trim());
    }
    public static string NormalizeAndValidate(string barcode)
    {
        var value = barcode.Trim();
        if (value.Length is not (8 or 12 or 13 or 14) || value.Any(character => !char.IsAsciiDigit(character)))
            throw new DomainValidationException("Barcode must be a valid EAN-8, UPC-A, EAN-13, or GTIN-14 value.");
        var sum = 0; var weight = 3;
        for (var index = value.Length - 2; index >= 0; index--) { sum += (value[index] - '0') * weight; weight = weight == 3 ? 1 : 3; }
        var checkDigit = (10 - (sum % 10)) % 10;
        if (checkDigit != value[^1] - '0') throw new DomainValidationException("Barcode check digit is invalid.");
        return value;
    }
}
