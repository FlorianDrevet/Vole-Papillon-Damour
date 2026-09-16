using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBook;

public sealed class GetAdminRareBookQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetAdminRareBookQuery, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        GetAdminRareBookQuery query,
        CancellationToken cancellationToken)
    {
        var rareBook = await dbContext.RareBooks
            .AsNoTracking()
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == query.RareBookId, cancellationToken);

        return rareBook is null
            ? Errors.RareBook.NotFound(query.RareBookId.Value)
            : RareBookProjector.ToResult(rareBook);
    }
}
