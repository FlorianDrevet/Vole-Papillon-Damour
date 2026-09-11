using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.SetMyAlertStatus;

public sealed record SetMyAlertStatusCommand(
    Guid ExternalId,
    string Email,
    bool Enabled,
    string? FirstName = null,
    string? LastName = null) : IRequest<ErrorOr<MyAlertPreferencesResult>>;
