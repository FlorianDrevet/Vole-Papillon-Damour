using System.Text.Json.Serialization;

namespace ShopAppVpd.Dtos;

public sealed class AppUpdateManifest
{
    [JsonPropertyName("versionName")]
    public required string VersionName { get; init; }

    [JsonPropertyName("versionCode")]
    public required int VersionCode { get; init; }

    [JsonPropertyName("apkUrl")]
    public required string ApkUrl { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}
