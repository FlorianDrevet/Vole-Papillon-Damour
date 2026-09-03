using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.Books.Commands.AssociationSettings;

public sealed class UpdateAssociationSettingsCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateAssociationSettingsCommand, ErrorOr<AssociationSettingsResult>>
{
    public async Task<ErrorOr<AssociationSettingsResult>> Handle(
        UpdateAssociationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (!AreValid(command))
        {
            return Errors.Book.InvalidAssociationSettings();
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var settings = await dbContext.AssociationSettings
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);

        if (settings is null)
        {
            settings = AssociationSettingsEntity.Create(command.UpdatedBy, updatedAt);
            dbContext.AssociationSettings.Add(settings);
        }

        settings.Update(
            command.DuplicateThreshold,
            command.DemandSalesThreshold,
            command.DeadStockMinAgeDays,
            command.DeadStockMinQuantity,
            command.WatchlistMaxItems,
            command.AlertCooldownDays,
            command.SessionIdleTimeoutMinutes,
            command.AlertDelayMinutes,
            command.UpdatedBy,
            updatedAt);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AssociationSettingsResult.From(settings);
    }

    private static bool AreValid(UpdateAssociationSettingsCommand command)
    {
        return command.DuplicateThreshold > 0 &&
               command.DemandSalesThreshold > 0 &&
               command.DeadStockMinAgeDays >= 0 &&
               command.DeadStockMinQuantity >= 0 &&
               command.WatchlistMaxItems > 0 &&
               command.AlertCooldownDays >= 0 &&
               command.SessionIdleTimeoutMinutes > 0 &&
               command.AlertDelayMinutes >= 0;
    }
}
