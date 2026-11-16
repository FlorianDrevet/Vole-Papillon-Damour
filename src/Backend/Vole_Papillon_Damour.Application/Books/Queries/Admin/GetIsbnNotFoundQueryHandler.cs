using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetIsbnNotFoundQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetIsbnNotFoundQuery, ErrorOr<IsbnNotFoundPageResult>>
{
    private const int MaximumPageSize = 200;

    public async Task<ErrorOr<IsbnNotFoundPageResult>> Handle(
        GetIsbnNotFoundQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaximumPageSize)
        {
            return Error.Validation(
                "Book.InvalidAdminPage",
                $"Page must be positive and page size must be between 1 and {MaximumPageSize}.");
        }

        var books = dbContext.Books
            .AsNoTracking()
            .Where(book => book.MetadataStatus == BookMetadataStatus.NotFound);

        var totalCount = await books.CountAsync(cancellationToken);
        var items = await books
            .OrderBy(book => book.LastAttemptAt)
            .ThenBy(book => book.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(book => new IsbnNotFoundBookResult(
                book.Id.Value,
                book.MetadataFailureReason,
                book.ResolveAttempts,
                book.LastAttemptAt.HasValue
                    ? new DateTimeOffset(book.LastAttemptAt.Value, TimeSpan.Zero)
                    : null))
            .ToListAsync(cancellationToken);

        return new IsbnNotFoundPageResult(items, totalCount, query.Page, query.PageSize);
    }
}
