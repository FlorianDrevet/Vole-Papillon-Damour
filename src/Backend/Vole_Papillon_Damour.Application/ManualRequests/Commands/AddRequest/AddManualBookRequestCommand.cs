using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.AddRequest;

public sealed record AddManualBookRequestCommand(
    Guid RequesterId,
    ManualBookRequestScope Scope,
    string? WorkId,
    string? Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    bool PickFromStock,
    UserId By) : IRequest<ErrorOr<ManualBookRequestCreatedResult>>;

public sealed record ManualBookRequestCreatedResult(Guid RequestId, ManualBookRequestStatus Status);
