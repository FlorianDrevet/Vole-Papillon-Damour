using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetBooksWithoutIsbn;

public sealed class GetBooksWithoutIsbnQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetBooksWithoutIsbnQuery, ErrorOr<BookWithoutIsbnPageResult>>
{
    public async Task<ErrorOr<BookWithoutIsbnPageResult>> Handle(
        GetBooksWithoutIsbnQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 200)
        {
            return Error.Validation("Book.InvalidPage", "La page doit être ≥ 1 et la taille entre 1 et 200.");
        }

        var books = dbContext.Books.AsNoTracking().Where(book => book.IsInternalCode);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            if (BookCode.TryCreate(search, out var code))
            {
                books = books.Where(book => book.Id == code ||
                    (book.Title != null && book.Title.Contains(search)) ||
                    (book.Authors != null && book.Authors.Contains(search)));
            }
            else
            {
                books = books.Where(book =>
                    (book.Title != null && book.Title.Contains(search)) ||
                    (book.Authors != null && book.Authors.Contains(search)));
            }
        }

        var total = await books.CountAsync(cancellationToken);
        var pageBooks = await books
            .Include(book => book.Photos)
            .OrderByDescending(book => book.FirstSeenAt)
            .ThenBy(book => book.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = pageBooks
            .Select(book => new BookWithoutIsbnItemResult(
                book.Id.Value,
                book.Title,
                book.Authors,
                book.Publisher,
                book.QuantityAvailable,
                book.FirstSeenAt,
                book.Photos
                    .OrderBy(photo => photo.Position)
                    .Select(photo => new BookPhotoResult(photo.Id, photo.BlobUri.AbsoluteUri, photo.Position, photo.ContentType))
                    .ToArray()))
            .ToArray();

        return new BookWithoutIsbnPageResult(total, query.Page, query.PageSize, items);
    }
}