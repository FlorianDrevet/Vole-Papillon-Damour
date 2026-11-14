using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

public static class FairDigestEmailContentBuilder
{
    public static FairDigestEmailContent Build(
        FairDigestEmailMessage message,
        FairDigestEmailLinks links)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentException.ThrowIfNullOrWhiteSpace(links.AssociationName);

        var view = FairDigestEmailLayout.Build(message);
        return new FairDigestEmailContent(
            view.Subject,
            view.Preheader,
            FairDigestEmailText.Render(view, links),
            FairDigestEmailHtml.Render(view, links));
    }
}
