using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed class RecordEmailBounceForRecipientCommandHandler(
    IProjectDbContext dbContext,
    ISender sender)
    : IRequestHandler<
        RecordEmailBounceForRecipientCommand,
        ErrorOr<RecordEmailBounceForRecipientResult>>
{
    public async Task<ErrorOr<RecordEmailBounceForRecipientResult>> Handle(
        RecordEmailBounceForRecipientCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Recipient) || command.Recipient.Trim().Length > 320)
        {
            return Errors.Watchlist.InvalidRecipient();
        }

        var recipient = command.Recipient.Trim();
        var member = await dbContext.Users
            .SingleOrDefaultAsync(user => user.Email == recipient, cancellationToken);

        if (member is null)
        {
            return new RecordEmailBounceForRecipientResult(
                RecordEmailBounceForRecipientOutcome.IgnoredUnknownRecipient);
        }

        var result = await sender.Send(
            new RecordEmailBounceCommand(member.Id, command.ProviderEventId),
            cancellationToken);

        return result.Match<ErrorOr<RecordEmailBounceForRecipientResult>>(
            bounce => new RecordEmailBounceForRecipientResult(
                bounce.AlreadyRecorded
                    ? RecordEmailBounceForRecipientOutcome.AlreadyRecorded
                    : RecordEmailBounceForRecipientOutcome.Recorded),
            errors => errors.Any(error => error.Code == "Watchlist.NotFound")
                ? new RecordEmailBounceForRecipientResult(
                    RecordEmailBounceForRecipientOutcome.IgnoredWithoutWatchlist)
                : errors);
    }
}
