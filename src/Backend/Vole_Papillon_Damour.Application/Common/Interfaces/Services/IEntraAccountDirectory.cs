using Vole_Papillon_Damour.Application.AccountAdministration;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IEntraAccountDirectory
{
    Task<IReadOnlyList<EntraAccount>> ListAsync(CancellationToken cancellationToken);

    Task<EntraAccount> CreateAsync(
        string email,
        string displayName,
        string temporaryPassword,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);

    Task<EntraAccount> SetRolesAsync(
        string externalId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);
}
