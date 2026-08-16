using GameCollector.Infrastructure.Persistence.Converters;

namespace GameCollector.Infrastructure.Tests;

public sealed class UtcDateTimeConverterTests
{
    private readonly UtcDateTimeConverter _converter = new();

    [Fact]
    public void LocalValuesAreConvertedToUtcBeforeStorage()
    {
        var localValue = new DateTime(2026, 8, 15, 12, 30, 0, DateTimeKind.Local);

        var storedValue = _converter.ConvertToProviderExpression.Compile()(localValue);

        Assert.Equal(DateTimeKind.Utc, storedValue.Kind);
        Assert.Equal(localValue.ToUniversalTime(), storedValue);
    }

    [Fact]
    public void ReadValuesAreMarkedAsUtc()
    {
        var storedValue = new DateTime(2026, 8, 15, 10, 30, 0, DateTimeKind.Unspecified);

        var domainValue = _converter.ConvertFromProviderExpression.Compile()(storedValue);

        Assert.Equal(DateTimeKind.Utc, domainValue.Kind);
        Assert.Equal(storedValue.Ticks, domainValue.Ticks);
    }
}
