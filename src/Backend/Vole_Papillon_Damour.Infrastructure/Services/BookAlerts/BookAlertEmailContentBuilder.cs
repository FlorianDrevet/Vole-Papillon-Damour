using System.Globalization;
using System.Net;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

public sealed record BookAlertEmailContent(
    string Subject,
    string PlainText,
    string Html);

public static class BookAlertEmailContentBuilder
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly TimeZoneInfo ParisTimeZone = FindParisTimeZone();

    public static BookAlertEmailContent Build(
        BookAlertDelivery delivery,
        string associationName,
        string? unsubscribeUrl)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentException.ThrowIfNullOrWhiteSpace(associationName);

        var rareItems = delivery.RareItems ?? [];
        var subject = BuildSubject(delivery.Items.Count, rareItems.Count);
        var greeting = string.IsNullOrWhiteSpace(delivery.RecipientName)
            ? "Bonjour,"
            : $"Bonjour {delivery.RecipientName},";
        var plainItems = delivery.Items
            .Select(BuildPlainItem)
            .Concat(rareItems.Select(BuildPlainRareItem))
            .ToArray();
        var htmlItems = delivery.Items
            .Select(BuildHtmlItem)
            .Concat(rareItems.Select(BuildHtmlRareItem))
            .ToArray();
        var plainUnsubscribe = string.IsNullOrWhiteSpace(unsubscribeUrl)
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}Se désabonner : {unsubscribeUrl}";
        var htmlUnsubscribe = string.IsNullOrWhiteSpace(unsubscribeUrl)
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(unsubscribeUrl)}\">Se désabonner</a></p>";

        var plainText = string.Join(
            Environment.NewLine,
            [
                greeting,
                string.Empty,
                $"{associationName} a {(rareItems.Count > 0 && delivery.Items.Count == 0 ? "signalé" : "trouvé")} :",
                string.Join(Environment.NewLine, plainItems),
                string.Empty,
                "La disponibilité doit être confirmée sur place ; aucune réservation ni mise de côté n'est effectuée.",
                $"{plainUnsubscribe}"
            ]);
        var htmlGreeting = string.IsNullOrWhiteSpace(delivery.RecipientName)
            ? "Bonjour,"
            : $"Bonjour {WebUtility.HtmlEncode(delivery.RecipientName)},";
        var html =
            $"<p>{htmlGreeting}</p>" +
            $"<p>{WebUtility.HtmlEncode(associationName)} a {(rareItems.Count > 0 && delivery.Items.Count == 0 ? "signalé" : "trouvé")} :</p>" +
            $"<ul>{string.Join(string.Empty, htmlItems)}</ul>" +
            "<p>La disponibilité doit être confirmée sur place ; aucune réservation ni mise de côté " +
            $"n'est effectuée.</p>{htmlUnsubscribe}";

        return new BookAlertEmailContent(subject, plainText, html);
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

    private static string BuildPlainItem(BookAlertOutboxItem item)
    {
        var baseLine =
            $"- {item.Title ?? "Titre non renseigné"} ({item.Isbn13.Value}) — " +
            $"{item.Quantity} exemplaire(s)";
        var edition = FormatEdition(item);
        var availability = FormatAvailability(item);
        return $"{baseLine}." +
               (edition is null ? string.Empty : $" {edition}.") +
               $" {availability}.";
    }

    private static string BuildHtmlItem(BookAlertOutboxItem item)
    {
        var baseLine =
            $"<strong>{WebUtility.HtmlEncode(item.Title ?? "Titre non renseigné")}</strong> " +
            $"({WebUtility.HtmlEncode(item.Isbn13.Value)}) — {item.Quantity} exemplaire(s).";
        var edition = FormatEdition(item);
        var editionHtml = edition is null
            ? string.Empty
            : $" <span>{WebUtility.HtmlEncode(edition)}.</span>";
        var availability =
            $" <span>{WebUtility.HtmlEncode(FormatAvailability(item))}.</span>";
        return $"<li>{baseLine}{editionHtml}<br>{availability}</li>";
    }

    private static string BuildPlainRareItem(RareBookAlertOutboxItem item)
    {
        var author = string.IsNullOrWhiteSpace(item.AuthorMention)
            ? string.Empty
            : $" — {item.AuthorMention.Trim()}";
        return $"- {item.Title}{author} : n'est plus disponible.";
    }

    private static string BuildHtmlRareItem(RareBookAlertOutboxItem item)
    {
        var title = WebUtility.HtmlEncode(item.Title);
        var author = string.IsNullOrWhiteSpace(item.AuthorMention)
            ? string.Empty
            : $" — {WebUtility.HtmlEncode(item.AuthorMention.Trim())}";
        return $"<li><strong>{title}</strong>{author} : n’est plus disponible.</li>";
    }

    private static string? FormatEdition(BookAlertOutboxItem item)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Publisher))
        {
            details.Add(item.Publisher.Trim());
        }

        if (item.PublicationYear is not null)
        {
            details.Add(item.PublicationYear.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(item.PhysicalFormat))
        {
            details.Add(item.PhysicalFormat.Trim());
        }

        return details.Count == 0
            ? null
            : $"Édition : {string.Join("; ", details)}";
    }

    private static string FormatAvailability(BookAlertOutboxItem item)
    {
        var formattedDate = item.FairOpeningAt is { } fairOpeningAt
            ? FormatDate(fairOpeningAt)
            : null;

        return item.Mode switch
        {
            ScanMode.NextFair when formattedDate is not null =>
                $"sera disponible à la bourse du {formattedDate}",
            ScanMode.NextFair => "sera disponible à une prochaine bourse",
            ScanMode.AvailableNow when formattedDate is not null =>
                $"disponible dès à présent. Prochaine ouverture : {formattedDate}",
            ScanMode.AvailableNow => "disponible dès à présent",
            _ => "disponibilité à confirmer"
        };
    }

    private static string FormatDate(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant, ParisTimeZone)
            .ToString("d MMMM yyyy", FrenchCulture);
    }

    private static TimeZoneInfo FindParisTimeZone()
    {
        foreach (var identifier in new[] { "Europe/Paris", "Romance Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(identifier);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
