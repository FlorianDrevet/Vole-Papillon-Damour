using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

internal static class BookPersistenceConversions
{
    public static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        value => RequireUtc(value),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTimeConverter = new(
        value => value.HasValue ? RequireUtc(value.Value) : null,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

    public static Isbn13 ParseIsbn13(string value)
    {
        if (Isbn13.TryCreate(value, out var isbn13))
        {
            return isbn13;
        }

        throw new InvalidOperationException($"The database contains an invalid ISBN-13 value: '{value}'.");
    }

    public static Isbn13? ParseNullableIsbn13(string? value)
    {
        return value is null ? null : ParseIsbn13(value);
    }

    public static string? SerializeNullableIsbn13(Isbn13? value)
    {
        return value?.Value;
    }

    private static DateTime RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must be expressed in UTC.", nameof(value));
        }

        return value;
    }
}
