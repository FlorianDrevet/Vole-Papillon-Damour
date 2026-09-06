namespace Vole_Papillon_Damour.Infrastructure.AccountDeletion;

public sealed class EntraGraphOptions
{
    public const string SectionName = "EntraGraph";

    public string TenantId { get; set; } = string.Empty;
    public string TenantDomain { get; set; } = string.Empty;
    public string ApiClientId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
