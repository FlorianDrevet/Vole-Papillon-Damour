using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed record RecordEmailBounceCommand(
    UserId MemberId,
    string ProviderEventId) : IRequest<ErrorOr<RecordEmailBounceResult>>;
