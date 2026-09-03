using FluentValidation;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed class RecordEmailBounceCommandValidator : AbstractValidator<RecordEmailBounceCommand>
{
    public RecordEmailBounceCommandValidator()
    {
        RuleFor(command => command.MemberId).NotNull();
    }
}
