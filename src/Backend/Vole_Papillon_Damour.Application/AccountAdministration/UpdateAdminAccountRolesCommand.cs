using ErrorOr;
using FluentValidation;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed record UpdateAdminAccountRolesCommand(
    string TargetExternalId,
    string RequesterExternalId,
    IReadOnlyCollection<string>? Roles) : IRequest<ErrorOr<AdminAccountResult>>;

public sealed class UpdateAdminAccountRolesCommandValidator : AbstractValidator<UpdateAdminAccountRolesCommand>
{
    public UpdateAdminAccountRolesCommandValidator()
    {
        RuleFor(command => command.TargetExternalId).NotEmpty();
        RuleFor(command => command.RequesterExternalId).NotEmpty();
        RuleFor(command => command.Roles)
            .NotNull()
            .Must(AccountRoles.IsValid)
            .WithMessage("The selected account role is not supported.");
    }
}

public sealed class UpdateAdminAccountRolesCommandHandler(IEntraAccountDirectory directory)
    : IRequestHandler<UpdateAdminAccountRolesCommand, ErrorOr<AdminAccountResult>>
{
    public async Task<ErrorOr<AdminAccountResult>> Handle(
        UpdateAdminAccountRolesCommand command,
        CancellationToken cancellationToken)
    {
        var roles = AccountRoles.Normalize(command.Roles);
        if (command.Roles is null || !AccountRoles.IsValid(command.Roles))
        {
            return Error.Validation("Account.InvalidRole", "The selected account role is not supported.");
        }

        if (string.Equals(command.TargetExternalId.Trim(), command.RequesterExternalId.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !roles.Contains(AccountRoles.Administration, StringComparer.Ordinal))
        {
            return Error.Conflict(
                "Account.CannotRemoveOwnAdministration",
                "An administrator cannot remove their own administration role.");
        }

        try
        {
            var account = await directory.SetRolesAsync(
                command.TargetExternalId.Trim(),
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
