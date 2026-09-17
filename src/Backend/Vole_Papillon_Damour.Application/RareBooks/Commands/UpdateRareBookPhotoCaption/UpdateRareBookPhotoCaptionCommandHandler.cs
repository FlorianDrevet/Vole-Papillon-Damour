using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBookPhotoCaption;

public sealed class UpdateRareBookPhotoCaptionCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateRareBookPhotoCaptionCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        UpdateRareBookPhotoCaptionCommand command,
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
            .AsNoTracking()
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

        bool changed;
        try
        {
            changed = rareBook.UpdatePhotoCaption(
                command.RareBookPhotoId,
                command.Caption,
                nowUtc,
                command.UserId);
        }
        catch (ArgumentException exception)
        {
            return Errors.RareBook.InvalidData(exception.Message);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return RareBookProjector.ToResult(rareBook);
    }
}
