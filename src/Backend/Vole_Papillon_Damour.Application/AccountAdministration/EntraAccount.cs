namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed record EntraAccount(
    string ExternalId,
    string? Email,
    string? DisplayName,
    bool AccountEnabled,
    DateTime? CreatedAt,
    IReadOnlyCollection<string> Roles);
