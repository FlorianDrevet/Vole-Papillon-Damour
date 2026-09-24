using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.MarkRareBookSold;

public sealed record MarkRareBookSoldCommand(
    RareBookId RareBookId,
    ScanSessionId? ScanSessionId,
    AssoEventsId? AssoEventsId,
    DateTime OccurredAt,
    UserId UserId,
    Guid? CheckoutPassageId = null) : IRequest<ErrorOr<RareBookResult>>;
