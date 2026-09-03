using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.RegisterSale;

public sealed record RegisterSaleCommand(
    string Isbn,
    int Quantity,
    DateTime OccurredAt,
    UserId VolunteerId,
    Guid ClientGestureId) : IRequest<ErrorOr<RegisterSaleResult>>;
