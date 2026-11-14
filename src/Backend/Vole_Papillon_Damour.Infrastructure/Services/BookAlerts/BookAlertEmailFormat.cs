using System.Globalization;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>
/// Libellés français des alertes : dates et heures à l'heure de Paris, édition, quantité.
/// </summary>
public static class BookAlertEmailFormat
{
    public static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly TimeZoneInfo ParisTimeZone = FindParisTimeZone();

    /// <summary>« samedi 7 novembre 2026 ».</summary>
    public static string Date(DateTimeOffset instant)
    {
        return ToParis(instant).ToString("dddd d MMMM yyyy", French);
    }

    /// <summary>« samedi 14 mars ».</summary>
    public static string ShortDate(DateTimeOffset instant)
    {
        return ToParis(instant).ToString("dddd d MMMM", French);
    }

    /// <summary>« 9 h 00 – 17 h 00 », « À partir de 9 h 00 », « Jusqu'à 17 h 00 » ou null.</summary>
    public static string? Hours(DateTimeOffset? doorsOpenAt, DateTimeOffset? doorsCloseAt)
    {
        return (doorsOpenAt, doorsCloseAt) switch
        {
            ({ } open, { } close) => $"{Hour(open)} – {Hour(close)}",
            ({ } open, null) => $"À partir de {Hour(open)}",
            (null, { } close) => $"Jusqu'à {Hour(close)}",
            _ => null
        };
    }

    /// <summary>« Folio · 1999 · Poche », éléments connus seulement, ou null.</summary>
    public static string? Edition(BookAlertOutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Edition(item.Publisher, item.PublicationYear, item.PhysicalFormat);
    }

    public static string? Edition(string? publisher, int? publicationYear, string? physicalFormat)
    {
        var parts = new[]
            {
                publisher?.Trim(),
                publicationYear?.ToString(CultureInfo.InvariantCulture),
                physicalFormat?.Trim()
            }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }

    public static string Quantity(int quantity)
    {
        return quantity > 1 ? $"{quantity} exemplaires" : "1 exemplaire";
    }

    public static string Title(BookAlertOutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Title(item.Title);
    }

    public static string Title(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "Titre non renseigné" : title.Trim();

    public static string? Authors(BookAlertOutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Authors(item.Authors);
    }

    public static string? Authors(string? authors) =>
        string.IsNullOrWhiteSpace(authors) ? null : authors.Trim();

    /// <summary>RG-94, sans le gras : « Ce livre n'est pas mis de côté. Premier arrivé, premier servi. »</summary>
    public static string NoReservation(int bookCount)
    {
        return $"{NoReservationLead(bookCount)} Premier arrivé, premier servi.";
    }

    public static string NoReservationLead(int bookCount)
    {
        return bookCount > 1
            ? "Ces livres ne sont pas mis de côté."
            : "Ce livre n'est pas mis de côté.";
    }

    /// <summary>RG-95 : raison de l'envoi, au singulier pour un seul livre cité.</summary>
    public static string Reason(int mentionedCount)
    {
        return mentionedCount > 1
            ? "Vous recevez cet e-mail parce que ces livres figurent dans votre liste de recherche."
            : "Vous recevez cet e-mail parce que ce livre figure dans votre liste de recherche.";
    }

    private static string Hour(DateTimeOffset instant)
    {
        var local = ToParis(instant);
        return $"{local.Hour} h {local.Minute:00}";
    }

    private static DateTimeOffset ToParis(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant, ParisTimeZone);
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
