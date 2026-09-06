using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminBookQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetAdminBookQuery, ErrorOr<AdminBookResult>>
{
    public async Task<ErrorOr<AdminBookResult>> Handle(
        GetAdminBookQuery query,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(query.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(query.Isbn);
        }

        var book = await dbContext.Books.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == isbn13,
            cancellationToken);
        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        var movements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement => movement.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        return AdminQueryProjection.ToBookResult(book, announcements, movements);
    }
}
