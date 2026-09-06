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

        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book => query.Rare == null || book.IsRare == query.Rare)
            .Where(book => query.Hidden == null || book.IsHiddenFromCatalog == query.Hidden)
            .Where(book => string.IsNullOrWhiteSpace(query.MetadataStatus) ||
                           book.MetadataStatus.ToString() == query.MetadataStatus)
            .Where(book => query.Undated != true || dbContext.BookAnnouncements.Any(
                announcement => announcement.Isbn13 == book.Id &&
                                announcement.Status == BookAnnouncementStatus.Announced &&
                                announcement.AssoEventsId == null))
            .OrderByDescending(book => book.UpdatedAt)
            .ThenBy(book => book.Id)
            .ToListAsync(cancellationToken);

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            books = books
                .Where(book =>
                    book.Id.Value.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    book.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                    book.Authors?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                    book.Publisher?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                    book.WorkId?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        var totalCount = books.Count;
        var pageBooks = books
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var ids = pageBooks.Select(book => book.Id).ToArray();
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => ids.Contains(announcement.Isbn13))
            .ToListAsync(cancellationToken);
        var movements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement => ids.Contains(movement.Isbn13))
            .ToListAsync(cancellationToken);

        return new AdminBookPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            pageBooks
                .Select(book => AdminQueryProjection.ToBookResult(book, announcements, movements, false))
                .ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }
}
