using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.PublishRareBook;

public sealed class PublishRareBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<PublishRareBookCommand, ErrorOr<RareBookPublishResult>>
{
    public async Task<ErrorOr<RareBookPublishResult>> Handle(
        PublishRareBookCommand command,
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

        var rareBook = await dbContext.RareBooks
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == command.RareBookId, cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(command.RareBookId.Value);
        }

        if (rareBook.Status == Domain.RareBookAggregate.ValueObjects.RareBookStatus.Draft &&
            (string.IsNullOrWhiteSpace(rareBook.Title) || rareBook.Price <= 0))
        {
            return Errors.RareBook.CannotPublish(command.RareBookId.Value);
        }

        bool changed;
        try
        {
            changed = rareBook.Publish(command.UserId, nowUtc);
        }
        catch (ArgumentException exception)
        {
            return Errors.RareBook.InvalidData(exception.Message);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var warnings = rareBook.Photos.Count == 0
            ? new[] { "La fiche est publiée sans photo d'exemplaire." }
            : Array.Empty<string>();

        return new RareBookPublishResult(
            RareBookProjector.ToResult(rareBook),
            changed,
            warnings);
    }
}
