using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanBook;

public sealed record ScanBookCommand(
    ScanSessionId ScanSessionId,
    string Isbn,
    bool Kept,
    DateTime OccurredAt,
    Guid ClientGestureId) : IRequest<ErrorOr<ScanBookResult>>;
