using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.AttachUndatedAnnouncements;

public sealed class AttachUndatedAnnouncementsCommandValidator : AbstractValidator<AttachUndatedAnnouncementsCommand>
{
    public AttachUndatedAnnouncementsCommandValidator()
    {
        RuleFor(command => command.TargetAssoEventsId).NotNull();
    }
}
