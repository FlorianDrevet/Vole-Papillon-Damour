using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Authentication.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Authentication;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.Authentication.Queries.Login;

public class LoginQueryHandler(IJwtGenerator jwtGenerator, IUserRepository userRepository, IHashPassword hashPassword):
    IRequestHandler<LoginQuery, ErrorOr<AuthenticationResult>>
{
    public async Task<ErrorOr<AuthenticationResult>> Handle(LoginQuery query, CancellationToken cancellationToken)
    {
        if (userRepository.GetUserByEmail(query.Email) is not User user)
        {
            return Errors.Authentication.InvalidUsername();
        }
        
        var hashedPassword = hashPassword.GetHashedPassword(query.Password, user.Salt);
        if (user.Password != hashedPassword)
        {
            return Errors.Authentication.InvalidPassword();
        }
        
        var token = jwtGenerator.GenerateToken(user);
        
        return new AuthenticationResult(user, token);
    }
}