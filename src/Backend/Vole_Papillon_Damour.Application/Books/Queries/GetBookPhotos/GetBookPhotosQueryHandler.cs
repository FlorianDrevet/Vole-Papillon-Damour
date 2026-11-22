using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetBookPhotos;

public sealed class GetBookPhotosQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetBookPhotosQuery, ErrorOr<IReadOnlyList<BookPhotoResult>>>
{
    public async Task<ErrorOr<IReadOnlyList<BookPhotoResult>>> Handle(
        GetBookPhotosQuery query,
        CancellationToken cancellationToken)
    {
        if (!BookCode.TryCreate(query.Code, out var code))
        {
            return Errors.Book.InvalidIsbn(query.Code);
        }

        var book = await dbContext.Books
            .AsNoTracking()
            .Include(candidate => candidate.Photos)
            .SingleOrDefaultAsync(candidate => candidate.Id == code, cancellationToken);
        if (book is null)
        {
            return Errors.Book.NotFound(code.Value);
        }

        if (!book.IsInternalCode && book.MetadataStatus != BookMetadataStatus.NotFound)
        {
            return Error.Validation(
                "Book.PhotoNotAllowed",
                "Les photos sont disponibles seulement pour les ISBN sans notice bibliographique.");
        }

        return book.Photos
            .OrderBy(photo => photo.Position)
            .Select(photo => new BookPhotoResult(
                photo.Id,
                photo.BlobUri.AbsoluteUri,
                photo.Position,
                photo.ContentType))
            .ToArray();
    }
}
