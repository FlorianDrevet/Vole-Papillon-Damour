using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBook;

public sealed class DeleteRareBookCommandHandler(
    IProjectDbContext dbContext,
    IBlobService blobService)
    : IRequestHandler<DeleteRareBookCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(
        DeleteRareBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!RareBookCommandSupport.IsValidUser(command.UserId))
        {
            return Errors.RareBook.InvalidUser();
        }

        var rareBook = await dbContext.RareBooks
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == command.RareBookId, cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(command.RareBookId.Value);
        }

        foreach (var photo in rareBook.Photos.OrderBy(photo => photo.Position))
        {
            var deletedBlobName = await blobService.DeleteFileAsync(
                BlobContainer.RareBookPhotos,
                photo.BlobName);
            if (string.IsNullOrWhiteSpace(deletedBlobName))
            {
                return Errors.RareBook.EmptyPhotoBlobName();
            }
        }

        dbContext.RareBooks.Remove(rareBook);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
