namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IEntraUserDirectory
{
    Task DeleteAsync(string externalId, CancellationToken cancellationToken);

    Task UpdateDisplayNameAsync(
        string externalId,
        string displayName,
        CancellationToken cancellationToken);
}
