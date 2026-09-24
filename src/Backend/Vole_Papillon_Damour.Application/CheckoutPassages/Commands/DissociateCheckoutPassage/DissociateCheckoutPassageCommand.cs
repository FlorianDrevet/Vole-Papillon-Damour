using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Commands.DissociateCheckoutPassage;

public sealed record DissociateCheckoutPassageCommand(
    Guid CheckoutPassageId,
    UserId AdministratorId,
    string Reason) : IRequest<ErrorOr<Success>>;
