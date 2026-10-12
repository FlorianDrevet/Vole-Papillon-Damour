using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

/// <summary>
/// Écrit les rendus E1, E2, E3 et « rares seuls » dans le dossier désigné par
/// VPD_EMAIL_SAMPLES_DIR, pour les comparer aux maquettes. Sans la variable, ne fait rien.
/// </summary>
public sealed class BookAlertEmailSamples
{
    private static readonly Guid November = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b01");
    private static readonly Guid December = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b02");

    [Fact]
    public void WriteSamples()
    {
        var directory = Environment.GetEnvironmentVariable("VPD_EMAIL_SAMPLES_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var links = new BookAlertEmailLinks(
            "Vole Papillon d'Amour",
            "https://volepapillondamour.fr/icons/vpd_icon.png",
            "https://livres.volepapillondamour.fr",
            "https://volepapillondamour.fr",
            "https://livres.volepapillondamour.fr/desinscription");
        var rare = new RareBookAlertOutboxItem(
            Guid.NewGuid(),
            "Voyage au bout de la nuit",
            "Louis-Ferdinand Céline",
            "voyage-au-bout-de-la-nuit");
        var samples = new Dictionary<string, BookAlertDelivery>
        {
            ["E1-un-livre"] = Delivery([Item("9782070408504", "Le Petit Prince", "Antoine de Saint-Exupéry", "Folio", 1999, ScanMode.NextFair, November)]),
            ["E2-deux-bourses"] = Delivery(
            [
                Item("9782070360024", "L'Étranger", "Albert Camus", "Folio", 2005, ScanMode.NextFair, November, quantity: 2),
                Item("9782070342761", "La Horde du Contrevent", "Alain Damasio", "Folio SF", 2007, ScanMode.NextFair, November),
                Item("9782253096344", "Les Misérables, tome 1", "Victor Hugo", "Le Livre de Poche", 1998, ScanMode.NextFair, December)
            ]),
            ["E3-mixte"] = Delivery(
                [Item("9782080703569", "Pêcheur d'Islande", "Pierre Loti", "GF Flammarion", 1990, ScanMode.AvailableNow, November, cover: false)],
                [rare]),
            ["E4-rares-seuls"] = Delivery([], [rare])
        };

        foreach (var (name, delivery) in samples)
        {
            var content = BookAlertEmailContentBuilder.Build(delivery, links);
            File.WriteAllText(Path.Combine(directory, $"{name}.html"), content.Html);
            File.WriteAllText(Path.Combine(directory, $"{name}.txt"), content.PlainText);
        }

        Directory.GetFiles(directory, "*.html").Should().HaveCountGreaterThanOrEqualTo(4);
    }

    private static BookAlertDelivery Delivery(
        IReadOnlyList<BookAlertOutboxItem> items,
        IReadOnlyList<RareBookAlertOutboxItem>? rareItems = null)
    {
        return new BookAlertDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "marie@example.org",
            "Marie",
            items,
            rareItems ?? [],
            [
                new BookAlertFair(
                    November,
                    new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 11, 7, 16, 0, 0, TimeSpan.Zero),
                    "12 rue des Fleurs, 42170 Saint-Just"),
                new BookAlertFair(
                    December,
                    new DateTimeOffset(2026, 12, 5, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 12, 5, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 12, 5, 17, 0, 0, TimeSpan.Zero),
                    "12 rue des Fleurs, 42170 Saint-Just")
            ]);
    }

    private static BookAlertOutboxItem Item(
        string isbn,
        string title,
        string authors,
        string publisher,
        int year,
        ScanMode mode,
        Guid fairId,
        int quantity = 1,
        bool cover = true)
    {
        Isbn13.TryCreate(isbn, out var isbn13).Should().BeTrue();
        return new BookAlertOutboxItem(
            isbn13,
            null,
            title,
            authors,
            quantity,
            mode,
            mode == ScanMode.NextFair ? fairId : null,
            publisher,
            year,
            "Poche",
            null,
            cover ? $"https://covers.openlibrary.org/b/isbn/{isbn}-M.jpg" : null,
            $"/livres/exemple-{isbn}",
            fairId);
    }
}
