using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicBook;

public sealed class GetPublicBookQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetPublicBookQuery, ErrorOr<PublicCatalogBookResult>>
{
    public async Task<ErrorOr<PublicCatalogBookResult>> Handle(
        GetPublicBookQuery query,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(query.Isbn13, out var isbn13))
        {
            return DomainErrors.Book.InvalidIsbn(query.Isbn13);
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var book = await dbContext.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book is null || book.IsHiddenFromCatalog || book.RedirectedToIsbn13 is not null)
        {
            return DomainErrors.Book.NotFound(isbn13.Value);
        }

        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return PublicCatalogProjector
            .Project([book], announcements, fairs, nowUtc)
            .Single();
    }
}
