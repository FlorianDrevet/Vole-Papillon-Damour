using ErrorOr;
using FluentValidation;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed record CreateAdminAccountCommand(
    string Email,
    string DisplayName,
    string TemporaryPassword,
    IReadOnlyCollection<string>? Roles) : IRequest<ErrorOr<AdminAccountResult>>;

public sealed class CreateAdminAccountCommandValidator : AbstractValidator<CreateAdminAccountCommand>
{
    public CreateAdminAccountCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(command => command.TemporaryPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(256);
        RuleFor(command => command.Roles)
            .NotNull()
            .NotEmpty()
            .Must(AccountRoles.IsValid)
            .WithMessage("The selected account role is not supported.");
    }
}

public sealed class CreateAdminAccountCommandHandler(IEntraAccountDirectory directory)
    : IRequestHandler<CreateAdminAccountCommand, ErrorOr<AdminAccountResult>>
{
    public async Task<ErrorOr<AdminAccountResult>> Handle(
        CreateAdminAccountCommand command,
        CancellationToken cancellationToken)
    {
        var roles = AccountRoles.Normalize(command.Roles);
        if (command.Roles is null || command.Roles.Count == 0 || !AccountRoles.IsValid(command.Roles))
        {
            return command.Roles is {Count: 0}
                ? Error.Validation("Account.RoleRequired", "At least one account role must be selected.")
                : Error.Validation("Account.InvalidRole", "The selected account role is not supported.");
        }

        try
        {
            var account = await directory.CreateAsync(
                command.Email.Trim().ToLowerInvariant(),
                command.DisplayName.Trim(),
                command.TemporaryPassword,
                roles,
                cancellationToken);
            return AdminAccountResultMapping.ToResult(account);
        }
        catch (EntraAccountDirectoryException exception)
        {
            return GetAdminAccountsQueryHandler.ToDirectoryError(exception);
        }
    }
}
