using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class ForceBookAlertMessageCommandHandler(
    IBookAlertOutbox bookAlertOutbox,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ForceBookAlertMessageCommand, ErrorOr<AdminAlertOperationResult>>
{
    public async Task<ErrorOr<AdminAlertOperationResult>> Handle(
        ForceBookAlertMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MessageId == Guid.Empty || command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidAlertOperation", "A valid message and administrator are required.");
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The alert clock must be expressed in UTC.");
        }

        var changed = await bookAlertOutbox.ForcePendingAsync(
            command.MessageId,
            now,
            cancellationToken);
        return new AdminAlertOperationResult(
            command.MessageId,
            "Pending",
            changed > 0);
    }
}
