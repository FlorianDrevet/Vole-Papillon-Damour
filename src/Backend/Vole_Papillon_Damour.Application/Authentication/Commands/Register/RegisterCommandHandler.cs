using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Authentication.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Authentication;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Authentication.Commands.Register;

public class RegisterCommandHandler(
    IJwtGenerator jwtGenerator,
    IHashPassword hashPassword,
    IUserRepository userRepository) : IRequestHandler<RegisterCommand, ErrorOr<AuthenticationResult>>
{
    public async Task<ErrorOr<AuthenticationResult>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (userRepository.GetUserByEmail(command.Email) is not null)
        {
            return Errors.User.DuplicateEmailError();
        }
        
        var hashedPassword = hashPassword.GetHashedPassword(command.Password);
        var user = User
            .Create(command.Email, hashedPassword.Item2, new Name(command.Firstname, command.Lastname),hashedPassword.Item1);
        userRepository.AddUser(user);
        
        var token = jwtGenerator.GenerateToken(user);
        return new AuthenticationResult(user, token);
    }
}