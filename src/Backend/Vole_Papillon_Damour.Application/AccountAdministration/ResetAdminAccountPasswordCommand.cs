using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed record ResetAdminAccountPasswordCommand(string TargetExternalId)
    : IRequest<ErrorOr<AdminAccountPasswordResetResult>>;
