using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class CancelBookAlertMessageCommandHandler(
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<CancelBookAlertMessageCommand, ErrorOr<AdminAlertOperationResult>>
{
    public async Task<ErrorOr<AdminAlertOperationResult>> Handle(
        CancelBookAlertMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MessageId == Guid.Empty || command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidAlertOperation", "A valid message and administrator are required.");
        }

        var changed = await bookAlertOutbox.CancelPendingAsync(
            command.MessageId,
            cancellationToken);
        return new AdminAlertOperationResult(
            command.MessageId,
            "Cancelled",
            changed > 0);
    }
}
