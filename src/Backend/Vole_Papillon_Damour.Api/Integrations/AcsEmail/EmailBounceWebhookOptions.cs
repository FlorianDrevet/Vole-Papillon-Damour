namespace Vole_Papillon_Damour.Api.Integrations.AcsEmail;

public sealed class EmailBounceWebhookOptions
{
    public const string SectionName = "EmailBounceWebhook";
    public const string SharedSecretHeaderName = "X-Vpd-EventGrid-Secret";

    public string SharedSecret { get; set; } = string.Empty;
}
