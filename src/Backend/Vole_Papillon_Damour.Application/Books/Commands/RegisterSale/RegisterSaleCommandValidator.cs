using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.RegisterSale;

public sealed class RegisterSaleCommandValidator : AbstractValidator<RegisterSaleCommand>
{
    public RegisterSaleCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThan(0);
        RuleFor(command => command.OccurredAt)
            .Must(timestamp => timestamp.Kind == DateTimeKind.Utc)
            .WithMessage("OccurredAt must be expressed in UTC.");
        RuleFor(command => command.VolunteerId).NotNull();
        RuleFor(command => command.ClientGestureId).NotEmpty();
    }
}
