using FluentValidation;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanSession;

public sealed class OpenScanSessionCommandValidator : AbstractValidator<OpenScanSessionCommand>
{
    public OpenScanSessionCommandValidator()
    {
        RuleFor(command => command.VolunteerId).NotNull();
        RuleFor(command => command.Mode).IsInEnum();
        RuleFor(command => command.TargetAssoEventsId)
            .Null()
            .When(command => command.Mode == ScanMode.AvailableNow)
            .WithMessage("A fair target is only valid for NextFair sessions.");
    }
}
