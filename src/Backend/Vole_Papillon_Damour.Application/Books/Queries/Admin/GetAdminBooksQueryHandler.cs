using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminBooksQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminBooksQuery, ErrorOr<AdminBookPageResult>>
{
    private const string LikeEscapeCharacter = "\\";

    public async Task<ErrorOr<AdminBookPageResult>> Handle(
        GetAdminBooksQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > 200)
        {
            return Errors.Book.InvalidAdminPage();
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var booksQuery = dbContext.Books
            .AsNoTracking()
            .Where(book => query.Rare == null || book.IsRare == query.Rare)
            .Where(book => query.Hidden == null || book.IsHiddenFromCatalog == query.Hidden)
            .Where(book => string.IsNullOrWhiteSpace(query.MetadataStatus) ||
                           book.MetadataStatus.ToString() == query.MetadataStatus)
            .Where(book => query.Undated != true || dbContext.BookAnnouncements.Any(
                announcement => announcement.Isbn13 == book.Id &&
                                announcement.Status == BookAnnouncementStatus.Announced &&
                                announcement.AssoEventsId == null));

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Search, count and paging run in SQL; LIKE follows the column collation
            // (case-insensitive, and accent-insensitive on Title and Authors).
            var pattern = $"%{EscapeLikePattern(search)}%";
            booksQuery = booksQuery.Where(book =>
                EF.Functions.Like((string)(object)book.Id, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.Title ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.Authors ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.Publisher ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.WorkId ?? string.Empty, pattern, LikeEscapeCharacter));
        }

        var totalCount = await booksQuery.CountAsync(cancellationToken);
        var pageBooks = await booksQuery
            .OrderByDescending(book => book.UpdatedAt)
            .ThenBy(book => book.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var ids = pageBooks.Select(book => book.Id).ToArray();
        var announcements = ids.Length == 0
            ? []
            : await dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement => ids.Contains(announcement.Isbn13))
                .ToListAsync(cancellationToken);

        // The page projection does not include movements, so they are not loaded.
        return new AdminBookPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            pageBooks
                .Select(book => AdminQueryProjection.ToBookResult(book, announcements, [], false))
                .ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal)
            .Replace("[", LikeEscapeCharacter + "[", StringComparison.Ordinal);
    }
}
