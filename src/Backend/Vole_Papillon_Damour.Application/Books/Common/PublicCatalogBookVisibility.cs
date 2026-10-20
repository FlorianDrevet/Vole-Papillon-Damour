using System.Linq.Expressions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public static class PublicCatalogBookVisibility
{
    public static readonly Expression<Func<Book, bool>> Predicate = book =>
        !book.IsHiddenFromCatalog &&
        book.RedirectedToIsbn13 == null &&
        (book.MetadataStatus == BookMetadataStatus.Resolved ||
         book.MetadataStatus == BookMetadataStatus.Manual) &&
        book.Title != null &&
        book.Title.Trim() != string.Empty;

    public static bool IsVisible(Book book)
    {
        return !book.IsHiddenFromCatalog &&
               book.RedirectedToIsbn13 is null &&
               book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual &&
               !string.IsNullOrWhiteSpace(book.Title);
    }
}
