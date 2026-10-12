using System.Text;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>Version texte de l'alerte : même contenu et même ordre que le HTML (RG-99).</summary>
public static class BookAlertEmailText
{
    public static string Render(BookAlertEmailView view, BookAlertEmailLinks links)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(links);
        var text = new StringBuilder();
        text.Append(view.FirstName is null ? "Bonjour," : $"Bonjour {view.FirstName},").Append("\n\n");
        text.Append(view.Headline.EmphasizedTitle).Append(view.Headline.Text).Append(".\n");

        foreach (var group in view.Groups)
        {
            text.Append('\n')
                .Append((group.MainLinePrefix + group.MainLineEmphasis).ToUpper(BookAlertEmailFormat.French))
                .Append('\n');
            AppendLine(text, group.NextOpening);
            AppendLine(text, group.Hours is null ? null : $"Horaires : {group.Hours}");
            AppendLine(text, group.Address is null ? null : $"Adresse : {group.Address}");
            foreach (var item in group.Items)
            {
                var authors = BookAlertEmailFormat.Authors(item);
                var edition = BookAlertEmailFormat.Edition(item);
                var quantity = BookAlertEmailFormat.Quantity(item.Quantity);
                text.Append("\n- ").Append(BookAlertEmailFormat.Title(item));
                text.Append(authors is null ? string.Empty : $" — {authors}").Append('\n');
                text.Append("  ").Append(edition is null ? quantity : $"Édition : {edition} — {quantity}").Append('\n');
                AppendLine(text, links.BookUrl(item.PublicPath) is { } url ? $"  Voir la fiche : {url}" : null);
            }
        }

        if (view.RareItems.Count > 0)
        {
            text.Append("\nLIVRES RARES SUIVIS\n");
            foreach (var rare in view.RareItems)
            {
                var author = string.IsNullOrWhiteSpace(rare.AuthorMention) ? string.Empty : $" — {rare.AuthorMention.Trim()}";
                text.Append("- ").Append(rare.Title).Append(author).Append(" : n'est plus disponible.\n");
            }
        }

        if (view.BookCount > 0)
        {
            text.Append('\n').Append(BookAlertEmailFormat.NoReservation(view.BookCount)).Append('\n');
            text.Append("\nVoir ma liste de recherche : ").Append(links.SearchListUrl).Append('\n');
        }

        text.Append("\n--\n");
        text.Append(links.AssociationName).Append(" — Bourse aux livres\n");
        text.Append(links.WebsiteUrl).Append('\n');
        text.Append(BookAlertEmailFormat.Reason(view.MentionedCount)).Append('\n');
        AppendLine(text, links.Unsubscribe is null ? null : $"Se désabonner : {links.Unsubscribe}");
        return text.ToString();
    }

    private static void AppendLine(StringBuilder text, string? line)
    {
        if (line is not null)
        {
            text.Append(line).Append('\n');
        }
    }
}
