namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record FairDigestEmailContent(
    string Subject,
    string Preheader,
    string PlainText,
    string Html);
