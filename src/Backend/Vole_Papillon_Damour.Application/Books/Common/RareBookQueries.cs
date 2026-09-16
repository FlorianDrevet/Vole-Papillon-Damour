using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

internal static class RareBookQueries
{
    public static IQueryable<RareBook> PublishedAvailable(
        this IQueryable<RareBook> query)
    {
        return query.Where(book =>
            book.Status == RareBookStatus.Published &&
            !book.IsSold &&
            book.Isbn13 != null);
    }

}
