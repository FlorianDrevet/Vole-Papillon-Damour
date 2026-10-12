using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>Contenu d'une alerte, ordonné comme le message (RG-90 à RG-97).</summary>
public sealed record BookAlertEmailView(
    string? FirstName,
    BookAlertEmailHeadline Headline,
    string Preheader,
    IReadOnlyList<BookAlertEmailGroup> Groups,
    IReadOnlyList<RareBookAlertOutboxItem> RareItems,
    int BookCount)
{
    public int MentionedCount => BookCount + RareItems.Count;
}

/// <summary>Accroche : titre mis en italique (ou null) suivi du texte.</summary>
public sealed record BookAlertEmailHeadline(string? EmphasizedTitle, string Text);

/// <summary>
/// Encart d'une bourse et ses cartes. La ligne principale est
/// <see cref="MainLinePrefix"/> suivi de <see cref="MainLineEmphasis"/> en gras.
/// </summary>
public sealed record BookAlertEmailGroup(
    ScanMode Mode,
    string Label,
    string MainLinePrefix,
    string MainLineEmphasis,
    string? NextOpening,
    string? Hours,
    string? Address,
    IReadOnlyList<BookAlertOutboxItem> Items);

public static class BookAlertEmailLayout
{
    public static BookAlertEmailView Build(BookAlertDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var fairs = (delivery.Fairs ?? []).ToDictionary(fair => fair.AssoEventsId);
        var rareItems = delivery.RareItems ?? [];

        var slots = delivery.Items
            .GroupBy(item => (
                item.Mode,
                Key: item.Mode == ScanMode.AvailableNow
                    ? (object?)item.OpeningFairId
                    : item.OpeningFairId ?? (object?)item.FairOpeningAt))
            .Select(group =>
            {
                var first = group.First();
                var fair = first.OpeningFairId is { } fairId ? fairs.GetValueOrDefault(fairId) : null;
                var openingAt = first.OpeningFairId is null
                    ? first.Mode == ScanMode.AvailableNow ? null : first.FairOpeningAt
                    : fair?.OpeningAt;
                return (Mode: group.Key.Mode, Fair: fair, OpeningAt: openingAt, Items: group.ToArray());
            })
            .OrderBy(slot => slot.Mode == ScanMode.AvailableNow && slot.OpeningAt is null ? 0 : 1)
            .ThenBy(slot => slot.OpeningAt ?? DateTimeOffset.MinValue)
            .ThenBy(slot => slot.Mode)
            .ToArray();

        var groups = new List<BookAlertEmailGroup>(slots.Length);
        var nextFairGroups = 0;
        foreach (var slot in slots)
        {
            var date = slot.OpeningAt is { } opening ? BookAlertEmailFormat.Date(opening) : null;
            var label = slot.Mode == ScanMode.AvailableNow
                ? "Disponible maintenant"
                : nextFairGroups++ == 0 ? "Prochaine bourse" : "Bourse suivante";
            if (slots.Length > 1)
            {
                label += $" · {(slot.Items.Length > 1 ? $"{slot.Items.Length} livres" : "1 livre")}";
            }

            var (prefix, emphasis) = slot.Mode == ScanMode.AvailableNow
                ? (string.Empty, "Disponible dès maintenant")
                : date is null
                    ? (string.Empty, "Disponible à une prochaine bourse")
                    : ("Disponible à la bourse du ", date);
            groups.Add(new BookAlertEmailGroup(
                slot.Mode,
                label,
                prefix,
                emphasis,
                slot.Mode == ScanMode.AvailableNow && date is not null
                    ? $"Prochaine ouverture : {date}"
                    : null,
                slot.Fair is null
                    ? null
                    : BookAlertEmailFormat.Hours(slot.Fair.DoorsOpenAt, slot.Fair.DoorsCloseAt),
                slot.Fair?.Address,
                slot.Items));
        }

        var bookCount = delivery.Items.Count;
        return new BookAlertEmailView(
            string.IsNullOrWhiteSpace(delivery.RecipientName) ? null : delivery.RecipientName.Trim(),
            BuildHeadline(delivery.Items),
            BuildPreheader(groups, bookCount),
            groups,
            rareItems,
            bookCount);
    }

    private static BookAlertEmailHeadline BuildHeadline(IReadOnlyList<BookAlertOutboxItem> items)
    {
        return items.Count switch
        {
            0 => new BookAlertEmailHeadline(null, "Des nouvelles de vos livres rares suivis"),
            1 when !string.IsNullOrWhiteSpace(items[0].Title) =>
                new BookAlertEmailHeadline(items[0].Title!.Trim(), " vous attend"),
            1 => new BookAlertEmailHeadline(null, "Un livre de votre liste vous attend"),
            _ => new BookAlertEmailHeadline(null, $"{items.Count} livres de votre liste vous attendent")
        };
    }

    private static string BuildPreheader(IReadOnlyList<BookAlertEmailGroup> groups, int bookCount)
    {
        if (groups.Count == 0)
        {
            return "Des nouvelles de vos livres rares suivis";
        }

        var first = groups[0];
        var mainLine = first.MainLinePrefix.Length == 0
            ? first.MainLineEmphasis
            : $"À la bourse du {first.MainLineEmphasis}";
        return $"{mainLine} · {(bookCount > 1 ? $"{bookCount} titres" : "1 titre")}";
    }
}
