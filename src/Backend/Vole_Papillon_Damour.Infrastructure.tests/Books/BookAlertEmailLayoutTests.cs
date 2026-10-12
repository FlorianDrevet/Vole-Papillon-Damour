using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;
using static Vole_Papillon_Damour.Infrastructure.tests.Books.BookAlertEmailTestData;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

public sealed class BookAlertEmailLayoutTests
{
    [Fact]
    public void Format_UsesFrenchDatesAndParisDoorHours()
    {
        BookAlertEmailFormat.Date(NovemberFair.OpeningAt).Should().Be("samedi 7 novembre 2026");
        BookAlertEmailFormat.Hours(NovemberFair.DoorsOpenAt, NovemberFair.DoorsCloseAt)
            .Should().Be("9 h 00 – 17 h 00");
        BookAlertEmailFormat.Hours(NovemberFair.DoorsOpenAt, null).Should().Be("À partir de 9 h 00");
        BookAlertEmailFormat.Hours(null, null).Should().BeNull();
    }

    [Fact]
    public void Layout_PutsTheFairBeforeTheBooksForASingleTitle()
    {
        var view = BookAlertEmailLayout.Build(Delivery([NextFairItem("Le Petit Prince", NovemberFairId)]));

        view.FirstName.Should().Be("Marie");
        view.Headline.Should().Be(new BookAlertEmailHeadline("Le Petit Prince", " vous attend"));
        view.Preheader.Should().Be("À la bourse du samedi 7 novembre 2026 · 1 titre");
        var group = view.Groups.Should().ContainSingle().Subject;
        group.Label.Should().Be("Prochaine bourse");
        group.MainLinePrefix.Should().Be("Disponible à la bourse du ");
        group.MainLineEmphasis.Should().Be("samedi 7 novembre 2026");
        group.Hours.Should().Be("9 h 00 – 17 h 00");
        group.Address.Should().Be("12 rue des Fleurs, 42170 Saint-Just");
    }

    [Fact]
    public void Layout_GroupsBooksPerFairInChronologicalOrder()
    {
        var view = BookAlertEmailLayout.Build(Delivery(
        [
            NextFairItem("Les Misérables", DecemberFairId, "9782253096344"),
            NextFairItem("L'Étranger", NovemberFairId, "9782070360024"),
            NextFairItem("La Horde du Contrevent", NovemberFairId, "9782070342761")
        ]));

        view.Headline.Should().Be(new BookAlertEmailHeadline(null, "3 livres de votre liste vous attendent"));
        view.Preheader.Should().Be("À la bourse du samedi 7 novembre 2026 · 3 titres");
        view.Groups.Select(group => group.Label).Should().Equal(
            "Prochaine bourse · 2 livres",
            "Bourse suivante · 1 livre");
        view.Groups[1].MainLineEmphasis.Should().Be("samedi 5 décembre 2026");
        view.Groups[1].Hours.Should().BeNull();
        view.Groups[1].Address.Should().BeNull();
    }

    [Fact]
    public void Layout_AvailableNowWithoutKnownOpeningComesFirstAndShowsNoDetails()
    {
        var view = BookAlertEmailLayout.Build(Delivery(
        [
            NextFairItem("Le Petit Prince", NovemberFairId, "9782070408504"),
            AvailableNowItem("Pêcheur d'Islande", openingFairId: null)
        ]));

        var first = view.Groups[0];
        first.Label.Should().Be("Disponible maintenant · 1 livre");
        first.MainLineEmphasis.Should().Be("Disponible dès maintenant");
        first.NextOpening.Should().BeNull();
        first.Hours.Should().BeNull();
        first.Address.Should().BeNull();
    }

    [Fact]
    public void Layout_IgnoresTheQueuedOpeningOfAFairThatIsNoLongerActive()
    {
        var item = AvailableNowItem("Pêcheur d'Islande", openingFairId: Guid.NewGuid());

        var view = BookAlertEmailLayout.Build(Delivery([item]));

        view.Groups.Should().ContainSingle().Which.NextOpening.Should().BeNull();
    }

    [Fact]
    public void Layout_GroupsLegacyAvailableNowItemsWithoutTrustingTheirQueuedOpeningDates()
    {
        var first = AvailableNowItem("Pêcheur d'Islande", openingFairId: null) with
        {
            FairOpeningAt = NovemberFair.OpeningAt
        };
        var second = AvailableNowItem("Autre titre", openingFairId: null) with
        {
            FairOpeningAt = DecemberFair.OpeningAt
        };

        var view = BookAlertEmailLayout.Build(Delivery([first, second]));

        var group = view.Groups.Should().ContainSingle().Subject;
        group.Items.Should().HaveCount(2);
        group.NextOpening.Should().BeNull();
        group.Hours.Should().BeNull();
        group.Address.Should().BeNull();
    }
}
