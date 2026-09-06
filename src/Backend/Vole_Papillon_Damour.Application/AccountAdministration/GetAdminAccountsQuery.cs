using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed record GetAdminAccountsQuery(
    string? Search,
    int Page = 1,
    int PageSize = 50) : IRequest<ErrorOr<AdminAccountPageResult>>;

public sealed class GetAdminAccountsQueryHandler(
    IEntraAccountDirectory directory,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminAccountsQuery, ErrorOr<AdminAccountPageResult>>
{
    public async Task<ErrorOr<AdminAccountPageResult>> Handle(
        GetAdminAccountsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > 200)
        {
            return Error.Validation("Account.InvalidPage", "The account page is invalid.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Account.InvalidClock", "The account administration clock must be expressed in UTC.");
        }

        try
        {
            var accounts = await directory.ListAsync(cancellationToken);
            var search = query.Search?.Trim();
            var rows = accounts
                .Where(account => string.IsNullOrWhiteSpace(search) || Matches(account, search!))
                .OrderBy(account => account.DisplayName ?? account.Email ?? account.ExternalId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(account => account.Email, StringComparer.OrdinalIgnoreCase)
                .Select(AdminAccountResultMapping.ToResult)
                .ToArray();

            return new AdminAccountPageResult(
                new DateTimeOffset(generatedAt, TimeSpan.Zero),
                rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(),
                rows.Length,
                query.Page,
                query.PageSize);
        }
        catch (EntraAccountDirectoryException exception)
        {
            return ToDirectoryError(exception);
        }
    }

    private static bool Matches(EntraAccount account, string search)
    {
        return account.ExternalId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               account.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
               account.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    internal static Error ToDirectoryError(EntraAccountDirectoryException exception)
    {
        return exception.StatusCode switch
        {
            StatusCodes.Status404NotFound => Error.NotFound(
                "Account.NotFound",
                "The identity account was not found."),
            StatusCodes.Status409Conflict => Error.Conflict(
                "Account.AlreadyExists",
                "This account already exists in the identity directory."),
            _ => Error.Failure(
                "Account.DirectoryUnavailable",
                "The identity directory is temporarily unavailable.")
        };
    }
}
