using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Authentication.Common;

namespace Vole_Papillon_Damour.Application.Authentication.Queries.Login;

public record LoginQuery(string Email, string Password) : IRequest<ErrorOr<AuthenticationResult>>;