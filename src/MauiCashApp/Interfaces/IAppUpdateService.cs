using ShopAppVpd.Dtos;

namespace ShopAppVpd.Interfaces;

public interface IAppUpdateService
{
    Task<AppUpdateManifest?> GetAvailableUpdateAsync(CancellationToken cancellationToken);

    bool CanInstallPackages { get; }

    void OpenInstallPermissionSettings();

    Task DownloadAndInstallAsync(
        AppUpdateManifest update,
        IProgress<double> downloadProgress,
        CancellationToken cancellationToken);
}
