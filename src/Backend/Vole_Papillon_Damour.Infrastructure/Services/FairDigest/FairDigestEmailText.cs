using System.Text;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

/// <summary>Version texte en gardant l'ordre et les informations du message HTML.</summary>
public static class FairDigestEmailText
{
    public static string Render(FairDigestEmailView view, FairDigestEmailLinks links)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(links);
        var text = new StringBuilder();
        text.Append(string.IsNullOrWhiteSpace(view.Message.FirstName)
                ? "Bonjour, voici ce qui vous attend samedi."
                : $"Bonjour {view.Message.FirstName.Trim()}, voici ce qui vous attend samedi.")
            .Append("\n\n")
            .Append(view.Heading)
            .Append("\n\nPROCHAINE BOURSE\nÀ la bourse du ")
            .Append(view.FullFairDate)
            .Append("\n")
            .Append(view.Message.Fair.Name)
            .Append('\n');
        AppendLine(text, view.FairHours is null ? null : $"Horaires : {view.FairHours}");
        AppendLine(text, view.FairAddress is null ? null : $"Adresse : {view.FairAddress}");

        if (view.Message.Content.News.Count > 0)
        {
            text.Append("\nNOUVEAUTÉS POUR VOUS\nArrivés depuis la dernière bourse, choisis d'après vos coups de cœur.\n");
            foreach (var recommendation in view.Message.Content.News)
            {
                AppendNews(text, recommendation, links);
            }
        }

        if (view.Message.Content.Back.Count > 0)
        {
            text.Append("\nDE RETOUR À LA BOURSE\nDes livres très demandés, de nouveau disponibles.\n");
            foreach (var item in view.Message.Content.Back)
            {
                AppendBack(text, item, links);
            }
        }

        if (view.TotalBookCount > 0)
        {
            text.Append('\n')
                .Append(BookAlertEmailFormat.NoReservation(view.TotalBookCount))
                .Append("\n\nVoir toutes mes suggestions : ")
                .Append(links.SuggestionsUrl)
                .Append('\n');
        }

        text.Append("\n--\n")
            .Append(links.AssociationName)
            .Append(" — Bourse aux livres\n")
            .Append(links.WebsiteUrl)
            .Append("\nVous recevez cet e-mail parce que vous avez demandé une sélection avant chaque bourse.\n");
        AppendLine(text, links.UnsubscribeUrl is null ? null : $"Se désabonner : {links.UnsubscribeUrl}");
        return text.ToString();
    }

    private static void AppendNews(
        StringBuilder text,
        PersonalRecommendation recommendation,
        FairDigestEmailLinks links)
    {
        var book = recommendation.Book;
        var hearts = Math.Clamp(recommendation.Hearts, 0, 5);
        text.Append("\n")
            .Append(new string('♥', hearts))
            .Append(new string('♡', 5 - hearts))
            .Append(' ')
            .Append(hearts)
            .Append(" cœurs sur 5\n- ")
            .Append(BookAlertEmailFormat.Title(book.Title))
            .Append('\n');
        AppendLine(text, BookAlertEmailFormat.Authors(book.Authors));
        AppendLine(text, BookAlertEmailFormat.Edition(book.Publisher, book.PublicationYear, book.PhysicalFormat));
        text.Append(FairDigestEmailLayout.Reason(recommendation)).Append('\n');
        AppendLine(text, links.BookUrl(book) is { } bookUrl ? $"Voir la fiche : {bookUrl}" : null);
    }

    private static void AppendBack(
        StringBuilder text,
        FairDigestBackItem item,
        FairDigestEmailLinks links)
    {
        var book = item.Book;
        text.Append("\n[")
            .Append(item.IsVeryDemanded ? "Très demandé" : "De retour")
            .Append("] ")
            .Append(BookAlertEmailFormat.Title(book.Title))
            .Append('\n');
        AppendLine(text, BookAlertEmailFormat.Authors(book.Authors));
        AppendLine(text, BookAlertEmailFormat.Edition(book.Publisher, book.PublicationYear, book.PhysicalFormat));
        AppendLine(text, links.BookUrl(book) is { } bookUrl ? $"Voir la fiche : {bookUrl}" : null);
    }

    private static void AppendLine(StringBuilder text, string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            text.Append(line).Append('\n');
        }
    }
}
