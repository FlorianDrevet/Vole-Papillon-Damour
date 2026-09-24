using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.SetSelectionItemStatus;

public sealed record SetSelectionItemStatusCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    Guid ItemId,
    MemberSelectionStatus Status) : IRequest<ErrorOr<Updated>>;
