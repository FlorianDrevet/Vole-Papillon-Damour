using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Commands.AssociateCheckoutPassage;

public sealed record AssociateCheckoutPassageCommand(
    Guid CheckoutPassageId,
    string Credential,
    DateTime OccurredAt,
    UserId VolunteerId) : IRequest<ErrorOr<CheckoutPassageAssociationResult>>;

public sealed record CheckoutPassageAssociationResult(
    Guid CheckoutPassageId,
    string Status,
    string? DisplayLabel,
    bool AlreadyProcessed);