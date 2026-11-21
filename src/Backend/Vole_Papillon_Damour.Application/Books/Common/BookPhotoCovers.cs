using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

// Photo d'un livre sans ISBN servant de couverture publique (Q11, RG-181) : la première par position.
internal static class BookPhotoCovers
{
    public static async Task<Dictionary<string, string>> FirstPhotoUrlsAsync(
        IProjectDbContext dbContext,
        IReadOnlyCollection<BookCode> codes,
        CancellationToken cancellationToken)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        if (codes.Count == 0)
        {
            return urls;
        }

        var codeArray = codes.ToArray();
        var books = await dbContext.Books
            .AsNoTracking()
            .Include(book => book.Photos)
            .Where(book => codeArray.Contains(book.Id))
            .ToListAsync(cancellationToken);
        foreach (var book in books.Where(book => book.Photos.Count > 0))
        {
            urls[book.Id.Value] = book.Photos.OrderBy(photo => photo.Position).First().BlobUri.AbsoluteUri;
        }

        return urls;
    }
}