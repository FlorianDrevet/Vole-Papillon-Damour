using FluentValidation;

namespace Vole_Papillon_Damour.Application.Authentication.Queries.Login.Validators;

public class LoginQueryValidator : AbstractValidator<LoginQuery>
{
   public LoginQueryValidator()
   {
      RuleFor(x => x.Email).NotEmpty().EmailAddress();
      RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
   } 
}