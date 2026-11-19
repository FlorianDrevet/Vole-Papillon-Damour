using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.AddBookPhoto;

public sealed class AddBookPhotoCommandHandler(
    IProjectDbContext dbContext,
    IBlobService blobService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddBookPhotoCommand, ErrorOr<BookPhotoResult>>
{
    public async Task<ErrorOr<BookPhotoResult>> Handle(
        AddBookPhotoCommand command,
        CancellationToken cancellationToken)
    {
        if (!BookCode.TryCreate(command.Code, out var code))
        {
            return Errors.Book.InvalidIsbn(command.Code);
        }

        if (!code.IsInternal)
        {
            return Error.Validation(
                "Book.PhotoNotAllowed",
                "Seuls les livres sans ISBN créés sur la plateforme reçoivent des photos.");
        }

        if (command.UserId is null || command.UserId.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidPhotoUser", "A user identifier is required.");
        }

        var contentType = command.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!RareBookCommandSupport.IsSupportedPhotoType(contentType))
        {
            return Error.Validation("Book.InvalidPhotoType", "La photo doit être au format JPEG, PNG ou WebP.");
        }

        if (command.SizeBytes <= 0 || command.SizeBytes > BookPhoto.MaximumSizeBytes)
        {
            return Error.Validation("Book.InvalidPhotoSize", "La photo doit peser au plus 8 Mo.");
        }

        if (command.Stream is null || string.IsNullOrWhiteSpace(command.FileName))
        {
            return Error.Validation("Book.InvalidPhoto", "A photo stream and file name are required.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var book = await dbContext.Books
            .Include(candidate => candidate.Photos)
            .SingleOrDefaultAsync(candidate => candidate.Id == code, cancellationToken);
        if (book is null)
        {
            return Error.NotFound("Book.NotFound", $"Aucune fiche pour le code {code.Value}.");
        }

        if (book.Photos.Count >= BookPhoto.MaximumPerBook)
        {
            return Error.Validation(
                "Book.TooManyPhotos",
                $"Un livre accepte au plus {BookPhoto.MaximumPerBook} photos.");
        }

        var photoId = Guid.NewGuid();
        var blobName = $"sans-isbn/{code.Value}/{photoId:D}{RareBookCommandSupport.ExtensionFor(contentType, command.FileName)}";
        var blobUri = await blobService.UploadBookPhotoAsync(blobName, command.Stream, contentType, cancellationToken);

        BookPhoto photo;
        try
        {
            photo = BookPhoto.Create(
                photoId,
                code,
                blobUri,
                blobName,
                book.Photos.Count,
                contentType,
                command.SizeBytes,
                nowUtc,
                command.UserId);
            book.AddPhoto(photo, nowUtc);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Error.Validation("Book.InvalidPhoto", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new BookPhotoResult(photo.Id, photo.BlobUri.AbsoluteUri, photo.Position, photo.ContentType);
    }
}