using GameCollector.Domain.Catalog;
using GameCollector.Domain.Common;

namespace GameCollector.Domain.Tests;

public sealed class BarcodeTests
{
    [Theory]
    [InlineData("96385074")]
    [InlineData("887961751062")]
    [InlineData("4006381333931")]
    [InlineData("10012345000017")]
    public void SupportedGtinWithValidCheckDigitIsAccepted(string barcode)
    {
        Assert.Equal(barcode, GameBarcode.NormalizeAndValidate(barcode));
    }

    [Theory]
    [InlineData("4006381333932")]
    [InlineData("abc")]
    [InlineData("123456789")]
    public void InvalidBarcodeIsRejected(string barcode)
    {
        Assert.Throws<DomainValidationException>(() => GameBarcode.NormalizeAndValidate(barcode));
    }
}
