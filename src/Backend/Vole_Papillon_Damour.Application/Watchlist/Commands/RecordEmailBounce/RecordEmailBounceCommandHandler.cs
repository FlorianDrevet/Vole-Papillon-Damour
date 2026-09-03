using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed class RecordEmailBounceCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RecordEmailBounceCommand, ErrorOr<RecordEmailBounceResult>>
{
    public async Task<ErrorOr<RecordEmailBounceResult>> Handle(
        RecordEmailBounceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MemberId is null || command.MemberId.Value == Guid.Empty)
        {
            return Error.Validation(
                "Watchlist.InvalidMemberId",
                "A valid member identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(command.ProviderEventId) ||
            command.ProviderEventId.Trim().Length > EmailBounceEvent.MaxProviderEventIdLength)
        {
            return Errors.Watchlist.InvalidProviderEventId();
        }

        var recordedAt = dateTimeProvider.UtcNow;
        if (recordedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Watchlist.InvalidBounceTimestamp();
        }

        var providerEventId = command.ProviderEventId.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var watchlist = await dbContext.Watchlists
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.MemberId,
                cancellationToken);
        if (watchlist is null)
        {
            return Errors.Watchlist.NotFound(command.MemberId.Value);
        }

        var existingEvent = await dbContext.EmailBounceEvents
            .SingleOrDefaultAsync(
                candidate => candidate.ProviderEventId == providerEventId,
                cancellationToken);
        if (existingEvent is not null)
        {
            if (existingEvent.UserId != command.MemberId)
            {
                return Errors.Watchlist.ProviderEventMemberMismatch(providerEventId);
            }

            await transaction.CommitAsync(cancellationToken);
            return RecordEmailBounceResult.From(watchlist, alreadyRecorded: true);
        }

        watchlist.RecordEmailBounce(recordedAt);
        dbContext.EmailBounceEvents.Add(EmailBounceEvent.Create(
            Guid.NewGuid(),
            providerEventId,
            command.MemberId,
            recordedAt));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RecordEmailBounceResult.From(watchlist, alreadyRecorded: false);
    }
}
