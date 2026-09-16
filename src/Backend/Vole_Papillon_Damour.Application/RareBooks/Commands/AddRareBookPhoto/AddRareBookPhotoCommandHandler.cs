using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.AddRareBookPhoto;

public sealed class AddRareBookPhotoCommandHandler(
    IProjectDbContext dbContext,
    IBlobService blobService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddRareBookPhotoCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        AddRareBookPhotoCommand command,
        CancellationToken cancellationToken)
    {
        if (!RareBookCommandSupport.IsValidUser(command.UserId))
        {
            return Errors.RareBook.InvalidUser();
        }

        var contentType = command.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!RareBookCommandSupport.IsSupportedPhotoType(contentType))
        {
            return Errors.RareBook.InvalidPhotoType(command.ContentType ?? string.Empty);
        }

        if (command.SizeBytes <= 0 || command.SizeBytes > RareBookCommandSupport.MaxPhotoSizeBytes)
        {
            return Errors.RareBook.InvalidPhotoSize(command.SizeBytes);
        }

        if (command.Stream is null || string.IsNullOrWhiteSpace(command.FileName))
        {
            return Errors.RareBook.InvalidData("A photo stream and file name are required.");
        }

        var clockError = RareBookCommandSupport.ValidateClock(dateTimeProvider, out var nowUtc);
        if (clockError is not null)
        {
            return clockError.Value;
        }

        var rareBook = await dbContext.RareBooks
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == command.RareBookId, cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(command.RareBookId.Value);
        }

        var photoId = RareBookPhotoId.CreateUnique();
        var blobName = $"{rareBook.Id.Value:D}/{photoId.Value:D}{RareBookCommandSupport.ExtensionFor(contentType, command.FileName)}";
        var blobUri = await blobService.UploadRareBookPhotoAsync(blobName, command.Stream);

        RareBookPhoto photo;
        try
        {
            photo = RareBookPhoto.CreateWithId(
                photoId,
                rareBook.Id,
                blobUri,
                blobName,
                command.Caption,
                rareBook.Photos.Count,
                contentType,
                command.SizeBytes,
                nowUtc,
                command.UserId);
            rareBook.AddPhoto(photo, nowUtc, command.UserId);
        }
        catch (ArgumentException exception)
        {
            return Errors.RareBook.InvalidData(exception.Message);
        }

        dbContext.RareBookPhotos.Add(photo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RareBookProjector.ToResult(rareBook);
    }
}
