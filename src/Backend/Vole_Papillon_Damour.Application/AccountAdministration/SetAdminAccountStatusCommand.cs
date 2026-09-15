using ErrorOr;
using FluentValidation;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed record SetAdminAccountStatusCommand(
    string TargetExternalId,
    string RequesterExternalId,
    bool AccountEnabled) : IRequest<ErrorOr<AdminAccountResult>>;

public sealed class SetAdminAccountStatusCommandValidator : AbstractValidator<SetAdminAccountStatusCommand>
{
    public SetAdminAccountStatusCommandValidator()
    {
        RuleFor(command => command.TargetExternalId).NotEmpty();
        RuleFor(command => command.RequesterExternalId).NotEmpty();
    }
}

public sealed class SetAdminAccountStatusCommandHandler(IEntraAccountDirectory directory)
    : IRequestHandler<SetAdminAccountStatusCommand, ErrorOr<AdminAccountResult>>
{
    public async Task<ErrorOr<AdminAccountResult>> Handle(
        SetAdminAccountStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                command.TargetExternalId.Trim(),
                command.RequesterExternalId.Trim(),
                StringComparison.OrdinalIgnoreCase) &&
            !command.AccountEnabled)
        {
            return Error.Conflict(
                "Account.CannotDisableOwnAccount",
                "An administrator cannot disable their own account.");
        }

        try
        {
            var account = await directory.SetAccountEnabledAsync(
                command.TargetExternalId.Trim(),
                command.AccountEnabled,
                cancellationToken);
            return AdminAccountResultMapping.ToResult(account);
        }
        catch (EntraAccountDirectoryException exception)
        {
            return GetAdminAccountsQueryHandler.ToDirectoryError(exception);
        }
    }
}
