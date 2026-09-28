using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBookByIdentifier;

public sealed class GetPublicRareBookByIdentifierQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetPublicRareBookByIdentifierQuery, ErrorOr<PublicRareBookDetailResult>>
{
    public async Task<ErrorOr<PublicRareBookDetailResult>> Handle(
        GetPublicRareBookByIdentifierQuery query,
        CancellationToken cancellationToken)
    {
        var publishedBooks = dbContext.RareBooks
            .AsNoTracking()
            .Include(book => book.Photos)
            .Where(book => book.Status == RareBookStatus.Published);

        RareBook? rareBook;
        if (Guid.TryParse(query.Identifier, out var idValue))
        {
            var id = RareBookId.Create(idValue);
            rareBook = await publishedBooks.SingleOrDefaultAsync(
                book => book.Id == id,
                cancellationToken);
        }
        else
        {
            RareBookSlug slug;
            try
            {
                slug = RareBookSlug.Create(query.Identifier);
            }
            catch (ArgumentException)
            {
                return Errors.RareBook.NotFound(query.Identifier);
            }

            rareBook = await publishedBooks.SingleOrDefaultAsync(
                book => book.Slug == slug,
                cancellationToken);
        }

        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(query.Identifier);
        }

        var relatedBooks = await dbContext.RareBooks
            .AsNoTracking()
            .Include(book => book.Photos)
            .Where(book => book.Status == RareBookStatus.Published &&
                           book.Id != rareBook.Id)
            .OrderByDescending(book => book.CreatedAt)
            .ThenBy(book => book.Id)
            .Take(3)
            .ToListAsync(cancellationToken);

        return new PublicRareBookDetailResult(
            RareBookProjector.ToPublicResult(rareBook),
            relatedBooks.Select(RareBookProjector.ToPublicResult).ToArray());
    }
}
