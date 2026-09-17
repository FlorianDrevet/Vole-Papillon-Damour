using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.UnsubscribeMemberAlerts;

/// <summary>
/// Suspends a member's alerts from the one-click unsubscribe endpoint. The
/// member is identified by a signed token, never by a request body, so the
/// command takes an already-verified identifier.
/// </summary>
public sealed record UnsubscribeMemberAlertsCommand(Guid MemberId)
    : IRequest<ErrorOr<UnsubscribeMemberAlertsResult>>;
