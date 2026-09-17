using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBookPhoto;

public sealed class DeleteRareBookPhotoCommandHandler(
    IProjectDbContext dbContext,
    IBlobService blobService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeleteRareBookPhotoCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        DeleteRareBookPhotoCommand command,
        CancellationToken cancellationToken)
    {
        if (!RareBookCommandSupport.IsValidUser(command.UserId))
        {
            return Errors.RareBook.InvalidUser();
        }

        var clockError = RareBookCommandSupport.ValidateClock(dateTimeProvider, out var nowUtc);
        if (clockError is not null)
        {
            return clockError.Value;
        }

        var photo = await dbContext.RareBookPhotos
            .SingleOrDefaultAsync(candidate => candidate.Id == command.RareBookPhotoId, cancellationToken);
        if (photo is null)
        {
            return Errors.RareBook.PhotoNotFound(command.RareBookPhotoId.Value);
        }

        var rareBook = await dbContext.RareBooks
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == photo.RareBookId, cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(photo.RareBookId.Value);
        }

        var deletedBlobName = await blobService.DeleteFileAsync(
            BlobContainer.RareBookPhotos,
            photo.BlobName);
        if (string.IsNullOrWhiteSpace(deletedBlobName))
        {
            return Errors.RareBook.EmptyPhotoBlobName();
        }

        if (!rareBook.RemovePhoto(command.RareBookPhotoId, nowUtc, command.UserId))
        {
            return Errors.RareBook.PhotoNotFound(command.RareBookPhotoId.Value);
        }

        dbContext.RareBookPhotos.Remove(photo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RareBookProjector.ToResult(rareBook);
    }
}
