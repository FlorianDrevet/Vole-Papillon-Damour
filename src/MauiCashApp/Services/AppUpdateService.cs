using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Content.PM;
using ShopAppVpd.Dtos;
using ShopAppVpd.Interfaces;
using ShopAppVpd.Platforms.Android;
using AndroidApplication = Android.App.Application;
using AndroidSettings = Android.Provider.Settings;

namespace ShopAppVpd.Services;

public sealed class AppUpdateService : IAppUpdateService
{
    private const string ManifestUrl = "https://backoffice.volepapillondamour.fr/apks/latest.json";
    private const string BackOfficeHost = "backoffice.volepapillondamour.fr";
    private const long MaximumApkSizeBytes = 100 * 1024 * 1024;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public AppUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanInstallPackages
    {
        get
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                return true;

            return AndroidApplication.Context.PackageManager?.CanRequestPackageInstalls() == true;
        }
    }

    public async Task<AppUpdateManifest?> GetAvailableUpdateAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(ManifestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var manifest = await response.Content.ReadFromJsonAsync<AppUpdateManifest>(
            ManifestJsonOptions,
            cancellationToken)
            ?? throw new InvalidDataException("The update manifest is empty.");

        ValidateManifest(manifest);
        if (manifest.VersionCode <= GetInstalledVersionCode())
            return null;

        return manifest;
    }

    public void OpenInstallPermissionSettings()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var context = AndroidApplication.Context;
        var settingsIntent = new Intent(AndroidSettings.ActionManageUnknownAppSources);
        settingsIntent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));
        settingsIntent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(settingsIntent);
    }

    public async Task DownloadAndInstallAsync(
        AppUpdateManifest update,
        IProgress<double> downloadProgress,
        CancellationToken cancellationToken)
    {
        if (!CanInstallPackages)
            throw new InvalidOperationException("Caisse is not allowed to install packages yet.");

        var apkUri = ValidateManifest(update);
        var context = AndroidApplication.Context;
        var updateDirectory = Path.Combine(context.CacheDir!.AbsolutePath!, "updates");
        Directory.CreateDirectory(updateDirectory);

        var safeVersion = string.Concat(update.VersionName.Where(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-'));
        if (string.IsNullOrWhiteSpace(safeVersion))
            throw new InvalidDataException("The update version name is invalid.");

        var apkPath = Path.Combine(updateDirectory, $"caisse-app-v{safeVersion}.apk");
        var temporaryPath = $"{apkPath}.download";

        try
        {
            await DownloadVerifiedApkAsync(apkUri, update.Sha256, temporaryPath, downloadProgress, cancellationToken);
            File.Move(temporaryPath, apkPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        await CommitInstallSessionAsync(context, apkPath, cancellationToken);
    }

    private static async Task CommitInstallSessionAsync(
        Context context,
        string apkPath,
        CancellationToken cancellationToken)
    {
        var apkFile = new Java.IO.File(apkPath);
        var packageInstaller = context.PackageManager!.PackageInstaller;
        var sessionParams = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
        sessionParams.SetAppPackageName(context.PackageName);
        sessionParams.SetSize(apkFile.Length());
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            sessionParams.SetRequireUserAction((int)PackageInstallUserAction.Required);

        var sessionId = packageInstaller.CreateSession(sessionParams);
        try
        {
            using var session = packageInstaller.OpenSession(sessionId);
            using var source = File.OpenRead(apkPath);
            using var destination = session.OpenWrite("caisse-update.apk", 0, source.Length);
            await source.CopyToAsync(destination, 81920, cancellationToken);
            session.Fsync(destination);

            var resultIntent = new Intent(context, typeof(AppUpdateInstallReceiver));
            resultIntent.SetAction($"{context.PackageName}.APP_UPDATE.{sessionId}");
            var pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
                pendingIntentFlags |= PendingIntentFlags.Mutable;

            var statusReceiver = PendingIntent.GetBroadcast(context, sessionId, resultIntent, pendingIntentFlags)
                ?? throw new InvalidOperationException("The Android package installer callback could not be created.");
            session.Commit(statusReceiver.IntentSender);
        }
        catch
        {
            packageInstaller.AbandonSession(sessionId);
            throw;
        }
    }

    private async Task DownloadVerifiedApkAsync(
        System.Uri apkUri,
        string expectedSha256,
        string destinationPath,
        IProgress<double> downloadProgress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, apkUri);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumApkSizeBytes)
            throw new InvalidDataException("The update package exceeds the supported size.");

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920];
        long bytesReceived = 0;
        var contentLength = response.Content.Headers.ContentLength;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                break;

            bytesReceived += bytesRead;
            if (bytesReceived > MaximumApkSizeBytes)
                throw new InvalidDataException("The update package exceeds the supported size.");

            hash.AppendData(buffer, 0, bytesRead);
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

            if (contentLength is > 0)
                downloadProgress.Report((double)bytesReceived / contentLength.Value);
        }

        if (bytesReceived == 0)
            throw new InvalidDataException("The update package is empty.");

        await destination.FlushAsync(cancellationToken);
        var expectedHash = Convert.FromHexString(expectedSha256);
        var actualHash = hash.GetHashAndReset();
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new InvalidDataException("The update package checksum does not match.");
    }

    private static System.Uri ValidateManifest(AppUpdateManifest manifest)
    {
        if (manifest.VersionCode <= 0 || string.IsNullOrWhiteSpace(manifest.VersionName))
            throw new InvalidDataException("The update manifest version is invalid.");

        if (!System.Uri.TryCreate(manifest.ApkUrl, UriKind.Absolute, out var apkUri) ||
            !string.Equals(apkUri.Scheme, System.Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(apkUri.Host, BackOfficeHost, StringComparison.OrdinalIgnoreCase) ||
            !apkUri.AbsolutePath.StartsWith("/apks/caisse-app-v", StringComparison.Ordinal) ||
            !apkUri.AbsolutePath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The update package URL is not allowed.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Sha256))
            throw new InvalidDataException("The update package checksum is invalid.");

        try
        {
            if (Convert.FromHexString(manifest.Sha256).Length != 32)
                throw new InvalidDataException("The update package checksum is invalid.");
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The update package checksum is invalid.", exception);
        }

        return apkUri;
    }

    private static long GetInstalledVersionCode()
    {
        var context = AndroidApplication.Context;
        var packageInfo = context.PackageManager?.GetPackageInfo(context.PackageName!, PackageInfoFlags.MetaData)
            ?? throw new InvalidOperationException("The installed package version could not be read.");
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            return packageInfo.LongVersionCode;

        return packageInfo.VersionCode;
    }
}
