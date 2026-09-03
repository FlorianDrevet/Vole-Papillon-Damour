using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.AssociationSettings;

public sealed class UpdateAssociationSettingsCommandValidator : AbstractValidator<UpdateAssociationSettingsCommand>
{
    public UpdateAssociationSettingsCommandValidator()
    {
        RuleFor(command => command.DuplicateThreshold).GreaterThan(0);
        RuleFor(command => command.DemandSalesThreshold).GreaterThan(0);
        RuleFor(command => command.DeadStockMinAgeDays).GreaterThanOrEqualTo(0);
        RuleFor(command => command.DeadStockMinQuantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.WatchlistMaxItems).GreaterThan(0);
        RuleFor(command => command.AlertCooldownDays).GreaterThanOrEqualTo(0);
        RuleFor(command => command.SessionIdleTimeoutMinutes).GreaterThan(0);
        RuleFor(command => command.AlertDelayMinutes).GreaterThanOrEqualTo(0);
        RuleFor(command => command.UpdatedBy).NotNull();
    }
}
