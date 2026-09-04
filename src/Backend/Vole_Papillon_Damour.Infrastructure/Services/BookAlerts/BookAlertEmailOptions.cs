namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

public sealed class BookAlertEmailOptions
{
    public const string SectionName = "BookAlerts:Email";

    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string MailFrom { get; set; } = "DoNotReply@mail.volepapillondamour.fr";
    public string? ManagedIdentityClientId { get; set; }
    public string AssociationName { get; set; } = "Vole Papillon d'Amour";
    public string? UnsubscribeUrl { get; set; }
}
