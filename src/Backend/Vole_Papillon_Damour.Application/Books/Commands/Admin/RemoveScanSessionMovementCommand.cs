using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record RemoveScanSessionMovementCommand(
    ScanSessionId ScanSessionId,
    Guid MovementId,
    UserId UpdatedBy) : IRequest<ErrorOr<AdminScanSessionOperationResult>>;
