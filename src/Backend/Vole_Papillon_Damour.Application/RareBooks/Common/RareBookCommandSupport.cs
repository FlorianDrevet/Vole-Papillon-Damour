using System.Collections;
using ErrorOr;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Common;

internal static class RareBookCommandSupport
{
    public const long MaxPhotoSizeBytes = 8 * 1024 * 1024;

    public static bool IsValidUser(UserId? userId) =>
        userId is not null && userId.Value != Guid.Empty;

    public static Error? ValidateClock(IDateTimeProvider dateTimeProvider, out DateTime utcNow)
    {
        utcNow = dateTimeProvider.UtcNow;
        return utcNow.Kind == DateTimeKind.Utc
            ? null
            : Errors.RareBook.InvalidTimestamp();
    }

    public static Error? ParseDetails(
        string conditionValue,
        string? isbnValue,
        out RareBookCondition condition,
        out Isbn13? isbn13)
    {
        condition = null!;
        isbn13 = null;

        try
        {
            condition = RareBookCondition.CreateFromString(conditionValue);
        }
        catch (ArgumentException exception)
        {
            return Errors.RareBook.InvalidData(exception.Message);
        }

        if (string.IsNullOrWhiteSpace(isbnValue))
        {
            return null;
        }

        if (!Isbn13.TryCreate(isbnValue, out var parsedIsbn13))
        {
            return Errors.RareBook.InvalidIsbn(isbnValue);
        }

        isbn13 = parsedIsbn13;
        return null;
    }

    public static bool MatchesRowVersion(byte[]? actual, byte[]? expected)
    {
        return actual is not null &&
               expected is not null &&
               StructuralComparisons.StructuralEqualityComparer.Equals(actual, expected);
    }

    public static string ExtensionFor(string contentType, string fileName)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/png" => ".png",
            _ => Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpeg" => ".jpg",
                ".jpg" => ".jpg",
                ".webp" => ".webp",
                ".png" => ".png",
                _ => ".bin"
            }
        };
    }

    public static bool IsSupportedPhotoType(string? contentType) =>
        contentType?.Trim().ToLowerInvariant() is "image/jpeg" or "image/webp" or "image/png";
}
