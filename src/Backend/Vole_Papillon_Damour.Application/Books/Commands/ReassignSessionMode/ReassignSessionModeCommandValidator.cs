using FluentValidation;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ReassignSessionMode;

public sealed class ReassignSessionModeCommandValidator : AbstractValidator<ReassignSessionModeCommand>
{
    public ReassignSessionModeCommandValidator()
    {
        RuleFor(command => command.ScanSessionId).NotNull();
        RuleFor(command => command.TargetMode).IsInEnum();
        RuleFor(command => command.TargetAssoEventsId)
            .Null()
            .When(command => command.TargetMode == ScanMode.AvailableNow)
            .WithMessage("AvailableNow sessions cannot target a fair.");
        RuleFor(command => command.UpdatedBy).NotNull();
    }
}
