using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBookBySlug;

public sealed class GetPublicRareBookBySlugQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetPublicRareBookBySlugQuery, ErrorOr<PublicRareBookDetailResult>>
{
    public async Task<ErrorOr<PublicRareBookDetailResult>> Handle(
        GetPublicRareBookBySlugQuery query,
        CancellationToken cancellationToken)
    {
        RareBookSlug slug;
        try
        {
            slug = RareBookSlug.Create(query.Slug);
        }
        catch (ArgumentException)
        {
            return Errors.RareBook.NotFound(query.Slug);
        }

        var rareBook = await dbContext.RareBooks
            .AsNoTracking()
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(
                book => book.Slug == slug &&
                        book.Status == RareBookStatus.Published,
                cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(query.Slug);
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
