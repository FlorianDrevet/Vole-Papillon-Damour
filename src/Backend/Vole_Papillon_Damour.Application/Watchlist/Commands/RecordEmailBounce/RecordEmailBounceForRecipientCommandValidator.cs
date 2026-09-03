using FluentValidation;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed class RecordEmailBounceForRecipientCommandValidator
    : AbstractValidator<RecordEmailBounceForRecipientCommand>
{
    public RecordEmailBounceForRecipientCommandValidator()
    {
        RuleFor(command => command.Recipient)
            .NotEmpty()
            .MaximumLength(320);
        RuleFor(command => command.ProviderEventId)
            .NotEmpty()
            .MaximumLength(EmailBounceEvent.MaxProviderEventIdLength);
    }
}
