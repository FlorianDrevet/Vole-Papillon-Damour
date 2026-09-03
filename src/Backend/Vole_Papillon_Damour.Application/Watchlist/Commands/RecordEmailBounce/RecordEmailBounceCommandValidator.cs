using FluentValidation;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed class RecordEmailBounceCommandValidator : AbstractValidator<RecordEmailBounceCommand>
{
    public RecordEmailBounceCommandValidator()
    {
        RuleFor(command => command.MemberId).NotNull();
        RuleFor(command => command.ProviderEventId)
            .NotEmpty()
            .MaximumLength(EmailBounceEvent.MaxProviderEventIdLength);
    }
}
