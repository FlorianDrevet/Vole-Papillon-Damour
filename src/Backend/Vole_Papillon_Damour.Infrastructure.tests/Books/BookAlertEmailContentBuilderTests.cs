using System.Net;
using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;
using static Vole_Papillon_Damour.Infrastructure.tests.Books.BookAlertEmailTestData;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

public sealed class BookAlertEmailContentBuilderTests
{
    [Fact]
    public void Text_CarriesTheSameContentAsTheHtml()
    {
        var content = BookAlertEmailContentBuilder.Build(
            Delivery([AvailableNowItem("Pêcheur d'Islande", NovemberFairId)]),
            Links);

        content.Subject.Should().Be("Un livre de votre liste de recherche est disponible");
        content.PlainText.Should().StartWith("Bonjour Marie,\n\nPêcheur d'Islande vous attend.\n");
        content.PlainText.Should().Contain("DISPONIBLE DÈS MAINTENANT\nProchaine ouverture : samedi 7 novembre 2026\n");
        content.PlainText.Should().Contain("Horaires : 9 h 00 – 17 h 00\nAdresse : 12 rue des Fleurs, 42170 Saint-Just\n");
        content.PlainText.Should().Contain("- Pêcheur d'Islande — Pierre Loti\n  Édition : GF Flammarion · 1990 · Poche — 1 exemplaire\n");
        content.PlainText.Should().Contain(
            "  Voir la fiche : https://livres.volepapillondamour.fr/livres/pecheur-d-islande-pierre-loti-9782080703569\n");
        content.PlainText.Should().Contain("Ce livre n'est pas mis de côté. Premier arrivé, premier servi.");
        content.PlainText.Should().Contain("Voir ma liste de recherche : https://livres.volepapillondamour.fr/compte");
        content.PlainText.Should().Contain("Vous recevez cet e-mail parce que ce livre figure dans votre liste de recherche.");
        content.PlainText.Should().EndWith("Se désabonner : https://livres.volepapillondamour.fr/desinscription\n");
    }

    [Fact]
    public void Html_IsAnEmailSafeSingleColumnLayout()
    {
        var content = BookAlertEmailContentBuilder.Build(
            Delivery([NextFairItem("Le Petit Prince", NovemberFairId)]),
            Links);

        content.Html.Should().StartWith("<!doctype html>");
        content.Html.Should().Contain("role=\"presentation\" width=\"600\"");
        content.Html.Should().Contain("mso-hide:all");
        content.Html.Should().Contain("@media (max-width:620px)");
        content.Html.Should().Contain("@media (prefers-color-scheme:dark)");
        content.Html.Should().NotContain("linear-gradient");
        content.Html.Should().NotContain("<script");
        content.Html.Should().Contain("src=\"https://volepapillondamour.fr/icons/vpd_icon.png\"");
        content.Html.Should().Contain("href=\"https://livres.volepapillondamour.fr/compte\"");
        var text = WebUtility.HtmlDecode(content.Html);
        text.Should().Contain("À la bourse du samedi 7 novembre 2026 · 1 titre");
        text.Should().Contain("<em>Le Petit Prince</em> vous attend");
        text.Should().Contain("Disponible à la bourse du <strong style=\"font-weight:600;\">samedi 7 novembre 2026");
        text.Should().Contain("Ce livre n'est pas mis de côté.");
        text.Should().Contain("Voir ma liste de recherche");
    }

    [Fact]
    public void Html_UsesReadableFooterLinkColorInDarkMode()
    {
        var content = BookAlertEmailContentBuilder.Build(
            Delivery([NextFairItem("Le Petit Prince", NovemberFairId)]),
            Links);

        content.Html.Should().Contain(".vpd-footer a{color:#c7d9e6!important;}");
    }

    [Fact]
    public void Html_ShowsAnHttpsCoverAndFallsBackToTheButterflyBlock()
    {
        var withCover = NextFairItem("Le Petit Prince", NovemberFairId) with
        {
            CoverUrl = "https://covers.example.org/petit-prince.jpg"
        };
        var insecureCover = NextFairItem("Contes du chat perché", NovemberFairId, "9782070612758") with
        {
            CoverUrl = "http://covers.example.org/chat.jpg"
        };

        var content = BookAlertEmailContentBuilder.Build(Delivery([withCover, insecureCover]), Links);

        content.Html.Should().Contain(
            "<img src=\"https://covers.example.org/petit-prince.jpg\" alt=\"Le Petit Prince\" width=\"88\" height=\"132\"");
        content.Html.Should().NotContain("http://covers.example.org/chat.jpg");
        content.Html.Should().Contain("class=\"vpd-cover vpd-panel\"");
    }

    [Fact]
    public void Html_EncodesCatalogText()
    {
        var item = NextFairItem("<Titre à vérifier>", NovemberFairId) with { Publisher = "Éditions & Fils" };

        var content = BookAlertEmailContentBuilder.Build(Delivery([item]), Links);

        content.Html.Should().Contain("&lt;Titre");
        content.Html.Should().Contain("&amp; Fils");
        content.Html.Should().NotContain("<Titre");
    }

    [Fact]
    public void RareOnlyAlert_HasNoFairNoNoticeAndNoButton()
    {
        var rare = new RareBookAlertOutboxItem(
            Guid.Parse("b7ef1e5e-8f64-4be3-b8d7-82f2d9e8b5fd"),
            "Voyage au bout de la nuit",
            "Louis-Ferdinand Céline",
            "voyage-au-bout-de-la-nuit");

        var content = BookAlertEmailContentBuilder.Build(Delivery([], [rare]), Links);

        content.Subject.Should().Be("Un livre rare suivi a été vendu");
        var html = WebUtility.HtmlDecode(content.Html);
        html.Should().Contain("Des nouvelles de vos livres rares suivis");
        html.Should().Contain("Livres rares suivis");
        html.Should().Contain("<em>Voyage au bout de la nuit</em> — Louis-Ferdinand Céline : n'est plus disponible.");
        html.Should().NotContain("pas mis de côté");
        html.Should().NotContain("Voir ma liste de recherche");
        html.Should().NotContain("class=\"vpd-panel\"");
        content.PlainText.Should().Contain("- Voyage au bout de la nuit — Louis-Ferdinand Céline : n'est plus disponible.");
    }

    [Fact]
    public void MixedAlert_ListsRareBooksAfterTheCards()
    {
        var rare = new RareBookAlertOutboxItem(Guid.NewGuid(), "Les Fleurs du mal", null, "les-fleurs-du-mal");

        var content = BookAlertEmailContentBuilder.Build(
            Delivery([NextFairItem("Le Petit Prince", NovemberFairId)], [rare]),
            Links);

        var html = WebUtility.HtmlDecode(content.Html);
        html.IndexOf("Livres rares suivis", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.IndexOf("Voir la fiche", StringComparison.Ordinal));
        html.IndexOf("Livres rares suivis", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("pas mis de côté", StringComparison.Ordinal));
        html.Should().Contain("ces livres figurent dans votre liste de recherche");
    }

    [Fact]
    public void WithoutFirstNameOrUnsubscribeUrl_TheMessageStaysComplete()
    {
        var delivery = Delivery([NextFairItem("Le Petit Prince", NovemberFairId)]) with { RecipientName = null };

        var content = BookAlertEmailContentBuilder.Build(delivery, Links with { UnsubscribeUrl = " " });

        content.PlainText.Should().StartWith("Bonjour,\n");
        content.Html.Should().Contain(">Bonjour,</p>");
        content.Html.Should().NotContain("Se désabonner");
        content.PlainText.Should().NotContain("Se désabonner");
    }
}
