using System.Net;
using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

namespace Vole_Papillon_Damour.Infrastructure.tests.FairDigest;

public sealed class FairDigestEmailHtmlTests
{
    private static readonly FairDigestEmailLinks Links = new(
        "Vole Papillon d'Amour",
        "https://volepapillondamour.fr/icons/vpd_icon.png",
        "https://livres.volepapillondamour.fr",
        "https://volepapillondamour.fr",
        "https://api.volepapillondamour.fr/integrations/email/unsubscribe?token=fair-token");

    [Fact]
    public void Build_RendersNewsReturnsHeartsReasonsAndLinksInOrder()
    {
        var content = new FairDigestMemberContent(
            [
                new PersonalRecommendation(Book("9782070363735", "Hypérion", "Dan Simmons"), NeighborReason.Theme, "Dune", 4),
                new PersonalRecommendation(Book("9782070408504", "Fondation", "Isaac Asimov"), NeighborReason.NextTome, "Dune", 3, 1),
            ],
            [new FairDigestBackItem(Book("9783140464079", "Le Petit Prince", "Antoine de Saint-Exupéry"), 12, IsVeryDemanded: true)],
            SkippedNoContent: false);

        var rendered = FairDigestEmailContentBuilder.Build(Message(content), Links);
        var html = WebUtility.HtmlDecode(rendered.Html);

        rendered.Subject.Should().Be("Avant la bourse du samedi 14 mars : 3 livres qui pourraient vous plaire");
        rendered.Preheader.Should().Be("2 nouveautés pour vous · 1 livre de retour");
        rendered.Html.Should().Contain("data-zone=\"E8-texte-apercu\"");
        rendered.Html.Should().Contain("data-zone=\"E8-objet\"");
        html.Should().Contain("Bonjour Marie, voici ce qui vous attend samedi.");
        html.Should().Contain("♥♥♥♥♡</span> 4 cœurs sur 5");
        html.Should().Contain("Parce que vous avez aimé Dune");
        html.Should().Contain("La suite de Dune · et 1 autre");
        html.Should().Contain("Très demandé");
        html.IndexOf("Nouveautés pour vous", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("De retour à la bourse", StringComparison.Ordinal));
        html.Should().Contain("Ces livres ne sont pas mis de côté.");
        html.Should().Contain("Voir toutes mes suggestions");
        html.Should().Contain("href=\"https://api.volepapillondamour.fr/integrations/email/unsubscribe?token=fair-token\"");
        html.Should().Contain("Vous recevez cet e-mail parce que vous avez demandé une sélection avant chaque bourse.");

        rendered.PlainText.Should().Contain("4 cœurs sur 5");
        rendered.PlainText.Should().Contain("La suite de Dune · et 1 autre");
        rendered.PlainText.Should().Contain("Voir toutes mes suggestions : https://livres.volepapillondamour.fr/trouver-mon-prochain-livre");
        rendered.PlainText.Should().Contain("Se désabonner : https://api.volepapillondamour.fr/integrations/email/unsubscribe?token=fair-token");
    }

    [Fact]
    public void Build_WithNoPersonalNewsShowsTheReturnsOnlyHeading()
    {
        var content = new FairDigestMemberContent(
            [],
            [
                new FairDigestBackItem(Book("9782070363735", "Livre 1", "Auteur 1"), 10, true),
                new FairDigestBackItem(Book("9783140464079", "Livre 2", "Auteur 2"), 8, true),
                new FairDigestBackItem(Book("9780306406157", "Livre 3", "Auteur 3"), 5, true),
            ],
            SkippedNoContent: false);

        var rendered = FairDigestEmailContentBuilder.Build(Message(content), Links);
        var html = WebUtility.HtmlDecode(rendered.Html);

        rendered.Subject.Should().Be("Avant la bourse du samedi 14 mars : 3 livres de retour");
        rendered.Preheader.Should().Be("3 livres de retour");
        html.Should().Contain("3 livres très demandés sont de retour");
        html.Should().Contain("data-zone=\"E11-email-sans-nouveautes\"");
        html.Should().NotContain("Nouveautés pour vous");
        html.Should().Contain("De retour à la bourse");
    }

    [Fact]
    public void Build_EncodesCatalogValuesAndUsesThePublicBookPath()
    {
        var item = Book("9782070363735", "<Hypérion & Cie>", "Dan Simmons");
        var content = new FairDigestMemberContent(
            [new PersonalRecommendation(item, NeighborReason.Theme, "Dune", 5)],
            [],
            SkippedNoContent: false);

        var rendered = FairDigestEmailContentBuilder.Build(Message(content), Links);
        var decodedHtml = WebUtility.HtmlDecode(rendered.Html);

        rendered.Html.Should().Contain("&lt;Hyp");
        decodedHtml.Should().Contain("<Hypérion & Cie>");
        rendered.Html.Should().Contain("https://livres.volepapillondamour.fr/livres/hyperion-cie-dan-simmons-9782070363735");
        rendered.Html.Should().NotContain("<Hypérion");
    }

    private static FairDigestEmailMessage Message(FairDigestMemberContent content) => new(
        Guid.Parse("a83c8586-6c92-40a4-a3ab-0f74515b20d7"),
        Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId.Create(
            Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de")),
        "marie@example.test",
        "Marie",
        new FairDigestEvent(
            Guid.Parse("d4e07b8c-9985-4ca7-929e-6f4f88c0b7c2"),
            EventsType.EventsTypeEnum.Books,
            DateTimeOffset.Parse("2026-03-14T09:00:00+01:00"),
            DateTimeOffset.Parse("2026-03-14T18:00:00+01:00"),
            false,
            "Bourse de printemps",
            "12 rue des Fleurs, 42170 Saint-Just"),
        content);

    private static Vole_Papillon_Damour.Application.Books.Common.PublicCatalogBookResult Book(
        string isbn,
        string title,
        string authors) => new(
        isbn,
        title,
        authors,
        "Éditions & Fils",
        2020,
        "Poche",
        "fr",
        "Roman",
        "OL123W",
        null,
        1,
        0,
        null,
        null,
        DateTimeOffset.Parse("2027-02-01T00:00:00Z"),
        DateTimeOffset.Parse("2027-02-01T00:00:00Z"),
        false);
}
