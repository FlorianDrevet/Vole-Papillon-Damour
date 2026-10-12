using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

/// <summary>Bourses, livres et liens d'exemple partagés par les tests du gabarit d'alerte.</summary>
internal static class BookAlertEmailTestData
{
    public static readonly Guid NovemberFairId = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b01");
    public static readonly Guid DecemberFairId = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b02");

    public static readonly BookAlertFair NovemberFair = new(
        NovemberFairId,
        new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 7, 16, 0, 0, TimeSpan.Zero),
        "12 rue des Fleurs, 42170 Saint-Just");

    public static readonly BookAlertFair DecemberFair = new(
        DecemberFairId,
        new DateTimeOffset(2026, 12, 5, 8, 0, 0, TimeSpan.Zero),
        null,
        null,
        null);

    public static readonly BookAlertEmailLinks Links = new(
        "Vole Papillon d'Amour",
        "https://volepapillondamour.fr/icons/vpd_icon.png",
        "https://livres.volepapillondamour.fr",
        "https://volepapillondamour.fr",
        "https://livres.volepapillondamour.fr/desinscription");

    public static BookAlertDelivery Delivery(
        IReadOnlyList<BookAlertOutboxItem> items,
        IReadOnlyList<RareBookAlertOutboxItem>? rareItems = null)
    {
        return new BookAlertDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "member@example.org",
            "Marie",
            items,
            rareItems ?? [],
            [NovemberFair, DecemberFair]);
    }

    public static BookAlertOutboxItem NextFairItem(string title, Guid fairId, string isbn = "9782070408504")
    {
        var fair = fairId == NovemberFairId ? NovemberFair : DecemberFair;
        return new BookAlertOutboxItem(
            ParseIsbn(isbn),
            null,
            title,
            "Antoine de Saint-Exupéry",
            1,
            ScanMode.NextFair,
            fairId,
            "Folio",
            1999,
            "Poche",
            fair.OpeningAt,
            PublicPath: $"/livres/le-petit-prince-{isbn}",
            OpeningFairId: fairId);
    }

    public static BookAlertOutboxItem AvailableNowItem(string title, Guid? openingFairId)
    {
        return new BookAlertOutboxItem(
            ParseIsbn("9782080703569"),
            null,
            title,
            "Pierre Loti",
            1,
            ScanMode.AvailableNow,
            null,
            "GF Flammarion",
            1990,
            "Poche",
            openingFairId is null ? null : NovemberFair.OpeningAt,
            PublicPath: "/livres/pecheur-d-islande-pierre-loti-9782080703569",
            OpeningFairId: openingFairId);
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
