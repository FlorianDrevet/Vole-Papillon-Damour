using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.AdjustQuantity;

public sealed record AdjustQuantityCommand(
    string Isbn,
    int QuantityAvailable,
    string Note,
    UserId VolunteerId) : IRequest<ErrorOr<AdjustQuantityResult>>;
