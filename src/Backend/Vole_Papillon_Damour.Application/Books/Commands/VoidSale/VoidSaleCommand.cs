using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.VoidSale;

public sealed record VoidSaleCommand(
    BookMovementId SaleMovementId,
    DateTime OccurredAt,
    UserId VolunteerId,
    Guid ClientGestureId) : IRequest<ErrorOr<VoidSaleResult>>;
