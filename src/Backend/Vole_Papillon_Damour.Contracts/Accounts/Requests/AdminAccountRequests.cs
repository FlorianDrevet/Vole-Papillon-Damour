namespace Vole_Papillon_Damour.Contracts.Accounts.Requests;

public sealed record CreateAdminAccountRequest(
    string Email,
    string DisplayName,
    string TemporaryPassword,
    IReadOnlyCollection<string>? Roles);

public sealed record UpdateAdminAccountRolesRequest(
    IReadOnlyCollection<string>? Roles);
