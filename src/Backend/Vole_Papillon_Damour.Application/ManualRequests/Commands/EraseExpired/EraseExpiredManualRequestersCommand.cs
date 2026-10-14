using MediatR;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.EraseExpired;

public sealed record EraseExpiredManualRequestersCommand : IRequest<int>;
