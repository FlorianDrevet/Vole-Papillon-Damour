namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IEntraAccountPasswordResetDirectory
{
    Task ResetPasswordAsync(
        string externalId,
        string temporaryPassword,
        CancellationToken cancellationToken);
}
