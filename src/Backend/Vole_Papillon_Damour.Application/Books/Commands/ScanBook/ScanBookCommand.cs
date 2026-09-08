using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanBook;

public sealed record ScanBookCommand(
    ScanSessionId ScanSessionId,
    string Isbn,
    bool Kept,
    DateTime OccurredAt,
    Guid ClientGestureId,
    UserId VolunteerId) : IRequest<ErrorOr<ScanBookResult>>;
