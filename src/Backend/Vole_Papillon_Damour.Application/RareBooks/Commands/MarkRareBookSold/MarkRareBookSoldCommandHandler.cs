using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.CheckoutPassages.Common;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.MarkRareBookSold;

public sealed class MarkRareBookSoldCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox,
    CheckoutPassageRecorder checkoutPassageRecorder,
    INotFoundReportLapser notFoundReportLapser)
    : IRequestHandler<MarkRareBookSoldCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        MarkRareBookSoldCommand command,
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

        if (command.OccurredAt.Kind != DateTimeKind.Utc)
        {
            return Errors.RareBook.InvalidTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var rareBook = await dbContext.RareBooks
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == command.RareBookId, cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(command.RareBookId.Value);
        }

        bool changed;
        try
        {
            changed = rareBook.MarkSold(
                command.OccurredAt,
                command.AssoEventsId,
                command.ScanSessionId,
                nowUtc,
                command.UserId);
        }
        catch (InvalidOperationException)
        {
            return Errors.RareBook.CannotSellDraft(command.RareBookId.Value);
        }
        catch (ArgumentException exception)
        {
            return Errors.RareBook.InvalidData(exception.Message);
        }

        var checkoutPassageId = command.CheckoutPassageId is { } requestedPassageId && requestedPassageId != Guid.Empty
            ? requestedPassageId
            : (Guid?)null;
        var passageLineAlreadyExists = !changed && checkoutPassageId is { } existingPassageId
            && await dbContext.CheckoutPassageLines.AnyAsync(
                line => line.CheckoutPassageId == existingPassageId && line.RareBookId == rareBook.Id,
                cancellationToken);

        if (checkoutPassageId is { } passageId && (changed || passageLineAlreadyExists))
        {
            await checkoutPassageRecorder.RecordRareSaleAsync(
                passageId, rareBook, command.OccurredAt, command.AssoEventsId, cancellationToken);
        }

        if (changed)
        {
            await notFoundReportLapser.LapseRareBookAsync(rareBook.Id, nowUtc, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await bookAlertOutbox.QueueRareBookSoldAsync(
                rareBook.Id,
                command.OccurredAt,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return RareBookProjector.ToResult(rareBook);
    }
}
