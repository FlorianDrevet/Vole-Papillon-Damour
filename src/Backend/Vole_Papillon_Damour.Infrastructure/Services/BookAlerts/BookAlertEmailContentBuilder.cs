using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

public sealed record BookAlertEmailContent(
    string Subject,
    string PlainText,
    string Html);

public static class BookAlertEmailContentBuilder
{
    public static BookAlertEmailContent Build(BookAlertDelivery delivery, BookAlertEmailLinks links)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentException.ThrowIfNullOrWhiteSpace(links.AssociationName);

        var subject = BuildSubject(delivery.Items.Count, delivery.RareItems?.Count ?? 0);
        var view = BookAlertEmailLayout.Build(delivery);
        return new BookAlertEmailContent(
            subject,
            BookAlertEmailText.Render(view, links),
            BookAlertEmailHtml.Render(view, links, subject));
    }

    private static string BuildSubject(int ordinaryItemCount, int rareItemCount)
    {
        if (ordinaryItemCount == 0 && rareItemCount == 1)
        {
            return "Un livre rare suivi a été vendu";
        }

        if (ordinaryItemCount == 0 && rareItemCount > 1)
        {
            return $"{rareItemCount} livres rares suivis ont été vendus";
        }

        if (rareItemCount == 0)
        {
            return ordinaryItemCount == 1
                ? "Un livre de votre liste de recherche est disponible"
                : $"{ordinaryItemCount} livres de votre liste de recherche sont disponibles";
        }

        return "Des livres suivis ont changé de disponibilité";
    }
}
