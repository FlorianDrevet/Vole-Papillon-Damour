namespace Vole_Papillon_Damour.Contracts.Accounts.Responses;

public sealed record AdminAccountResponse(
    string ExternalId,
    string? Email,
    string? DisplayName,
    bool AccountEnabled,
    DateTimeOffset? CreatedAt,
    IReadOnlyCollection<string> Roles);

public sealed record AdminAccountPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminAccountResponse> Accounts,
    int TotalCount,
    int Page,
    int PageSize);
