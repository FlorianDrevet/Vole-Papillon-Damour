using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

public sealed class BookAlertEmailContentBuilderTests
{
    [Fact]
    public void Build_NamesTheEditionAvailabilityAndNoReservationPolicy()
    {
        var item = new BookAlertOutboxItem(
            ParseIsbn("9782070363735"),
            "work-42",
            "<Titre à vérifier>",
            "Auteur",
            2,
            ScanMode.NextFair,
            null,
            "Éditions & Fils",
            2020,
            "Poche",
            new DateTimeOffset(2026, 3, 14, 10, 0, 0, TimeSpan.Zero));
        var delivery = new BookAlertDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "member@example.org",
            "Prénom Nom",
            [item]);

        var content = BookAlertEmailContentBuilder.Build(
            delivery,
            "Vole Papillon & Co",
            "https://example.org/desinscription");

        content.PlainText.Should().Contain("Édition : Éditions & Fils; 2020; Poche.");
        content.PlainText.Should().Contain("sera disponible à la bourse du 14 mars 2026");
        content.PlainText.Should().Contain("aucune réservation ni mise de côté");
        content.PlainText.Should().Contain("https://example.org/desinscription");
        content.Html.Should().Contain("&lt;Titre");
        content.Html.Should().Contain("&amp; Fils");
        content.Html.Should().Contain("aucune réservation ni mise de côté");
        content.Html.Should().Contain("href=\"https://example.org/desinscription\"");
    }

    [Fact]
    public void Build_ForAvailableNowIncludesTheNextFairOpeningWhenKnown()
    {
        var item = new BookAlertOutboxItem(
            ParseIsbn("9782070363735"),
            null,
            "Titre",
            null,
            1,
            ScanMode.AvailableNow,
            null,
            FairOpeningAt: new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));
        var delivery = new BookAlertDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "member@example.org",
            null,
            [item]);

        var content = BookAlertEmailContentBuilder.Build(delivery, "Association", null);

        content.PlainText.Should().Contain("disponible dès à présent");
        content.PlainText.Should().Contain("Prochaine ouverture : 14 septembre 2026");
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
