using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.Authentication.Common;

public record AuthenticationResult(
    User User,
    string Token);