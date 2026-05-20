using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Authentication.Common;

namespace Vole_Papillon_Damour.Application.Authentication.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string Firstname,
    string Lastname) : IRequest<ErrorOr<AuthenticationResult>>;