using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GameCollector.Infrastructure.Persistence.Converters;

public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime(),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
