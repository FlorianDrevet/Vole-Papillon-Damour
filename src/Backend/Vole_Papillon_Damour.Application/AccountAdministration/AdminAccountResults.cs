namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed record AdminAccountResult(
    string ExternalId,
    string? Email,
    string? DisplayName,
    bool AccountEnabled,
    DateTime? CreatedAt,
    IReadOnlyCollection<string> Roles);

public sealed record AdminAccountPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminAccountResult> Accounts,
    int TotalCount,
    int Page,
    int PageSize);

internal static class AdminAccountResultMapping
{
    public static AdminAccountResult ToResult(EntraAccount account) => new(
        account.ExternalId,
        account.Email,
        account.DisplayName,
        account.AccountEnabled,
        account.CreatedAt,
        account.Roles);
}
