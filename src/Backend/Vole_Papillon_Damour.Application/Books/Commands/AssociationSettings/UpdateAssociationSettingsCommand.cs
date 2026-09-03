using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.AssociationSettings;

public sealed record UpdateAssociationSettingsCommand(
    int DuplicateThreshold,
    int DemandSalesThreshold,
    int DeadStockMinAgeDays,
    int DeadStockMinQuantity,
    int WatchlistMaxItems,
    int AlertCooldownDays,
    int SessionIdleTimeoutMinutes,
    int AlertDelayMinutes,
    UserId UpdatedBy) : IRequest<ErrorOr<AssociationSettingsResult>>;
