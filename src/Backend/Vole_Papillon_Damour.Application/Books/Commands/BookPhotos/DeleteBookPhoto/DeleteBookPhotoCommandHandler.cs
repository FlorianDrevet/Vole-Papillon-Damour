using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.DeleteBookPhoto;

public sealed class DeleteBookPhotoCommandHandler(
    IProjectDbContext dbContext,
    IBlobService blobService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeleteBookPhotoCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(
        DeleteBookPhotoCommand command,
        CancellationToken cancellationToken)
    {
        if (!BookCode.TryCreate(command.Code, out var code))
        {
            return Errors.Book.InvalidIsbn(command.Code);
        }

        if (command.UserId is null || command.UserId.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidPhotoUser", "A user identifier is required.");
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

        var blobName = book.Photos.FirstOrDefault(photo => photo.Id == command.PhotoId)?.BlobName;
        if (blobName is null || !book.RemovePhoto(command.PhotoId, nowUtc))
        {
            return Error.NotFound("Book.PhotoNotFound", "Photo introuvable pour ce livre.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await blobService.DeleteFileAsync(BlobContainer.BookCovers, blobName);
        return Result.Deleted;
    }
}