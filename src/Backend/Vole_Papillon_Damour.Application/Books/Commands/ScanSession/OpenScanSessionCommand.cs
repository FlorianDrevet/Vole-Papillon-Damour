using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanSession;

public sealed record OpenScanSessionCommand(
    UserId VolunteerId,
    ScanMode Mode,
    AssoEventsId? TargetAssoEventsId) : IRequest<ErrorOr<ScanSessionResult>>;
