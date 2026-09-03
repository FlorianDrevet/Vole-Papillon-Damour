using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.VoidSale;

public sealed class VoidSaleCommandValidator : AbstractValidator<VoidSaleCommand>
{
    public VoidSaleCommandValidator()
    {
        RuleFor(command => command.SaleMovementId).NotNull();
        RuleFor(command => command.OccurredAt)
            .Must(timestamp => timestamp.Kind == DateTimeKind.Utc)
            .WithMessage("OccurredAt must be expressed in UTC.");
        RuleFor(command => command.VolunteerId).NotNull();
        RuleFor(command => command.ClientGestureId).NotEmpty();
    }
}
