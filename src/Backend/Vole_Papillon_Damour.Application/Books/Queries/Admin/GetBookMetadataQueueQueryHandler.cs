using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetBookMetadataQueueQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetBookMetadataQueueQuery, ErrorOr<BookMetadataQueuePageResult>>
{
    private const int MaximumPageSize = 200;

    public async Task<ErrorOr<BookMetadataQueuePageResult>> Handle(
        GetBookMetadataQueueQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaximumPageSize)
        {
            return Error.Validation(
                "Book.InvalidAdminPage",
                $"Page must be positive and page size must be between 1 and {MaximumPageSize}.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var queue = dbContext.Books
            .AsNoTracking()
            .Where(book => book.MetadataStatus != BookMetadataStatus.Discarded)
            .Where(book =>
                book.MetadataStatus == BookMetadataStatus.Pending ||
                book.MetadataStatus == BookMetadataStatus.Failed ||
                book.MetadataStatus == BookMetadataStatus.NotFound ||
                book.Title == null ||
                book.Title.Trim() == string.Empty);

        if (query.MetadataStatus is { } metadataStatus)
        {
            queue = queue.Where(book => book.MetadataStatus == metadataStatus);
        }

        var totalCount = await queue.CountAsync(cancellationToken);
        var items = await queue
            .OrderBy(book => book.LastAttemptAt)
            .ThenBy(book => book.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(book => new BookMetadataQueueItemResult(
                book.Id.Value,
                book.MetadataStatus.ToString(),
                book.MetadataFailureReason,
                book.ResolveAttempts,
                book.LastAttemptAt.HasValue
                    ? new DateTimeOffset(book.LastAttemptAt.Value, TimeSpan.Zero)
                    : null))
            .ToListAsync(cancellationToken);

        return new BookMetadataQueuePageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            items,
            totalCount,
            query.Page,
            query.PageSize);
    }
}
