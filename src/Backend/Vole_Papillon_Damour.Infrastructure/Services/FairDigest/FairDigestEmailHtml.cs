using System.Net;
using System.Text;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

/// <summary>Gabarit e-mail une colonne, aligné sur le rendu des alertes de bourse.</summary>
public static class FairDigestEmailHtml
{
    private const string Canvas = "#e6edf2";
    private const string Paper = "#ffffff";
    private const string PaperSoft = "#f7fbfe";
    private const string PaperBlue = "#e9f4fb";
    private const string Ink = "#041d30";
    private const string InkSoft = "#072b45";
    private const string Body = "#33546b";
    private const string Muted = "#4a6478";
    private const string Blue = "#0c6ea6";
    private const string Cyan = "#7fd8f5";
    private const string Orange = "#f0801c";
    private const string Line = "#d9e9f4";
    private const string LineSoft = "#e2eef7";
    private const string Heart = "#c8364e";
    private const string Serif = "Newsreader,Georgia,'Times New Roman',serif";
    private const string Sans = "'Libre Franklin',Arial,Helvetica,sans-serif";
    private const string Mono = "'IBM Plex Mono','Courier New',monospace";
    private const string ResponsiveStyles =
        "body{margin:0;padding:0;}" +
        "@media (max-width:620px){" +
        ".vpd-outer{padding:0!important;}.vpd-container{width:100%!important;border:0!important;}" +
        ".vpd-pad{padding-left:20px!important;padding-right:20px!important;}" +
        ".vpd-h1{font-size:28px!important;}.vpd-cover-cell{width:64px!important;}" +
        ".vpd-cover{width:64px!important;height:96px!important;}" +
        ".vpd-button{display:block!important;text-align:center!important;}" +
        "}" +
        "@media (prefers-color-scheme:dark){" +
        ".vpd-bg{background:#081520!important;}.vpd-container{background:#0b1b28!important;border-color:#23415a!important;}" +
        ".vpd-ink{color:#ffffff!important;}.vpd-body{color:#c7d9e6!important;}" +
        ".vpd-panel{background:#12304a!important;border-color:#23415a!important;}.vpd-card{border-color:#23415a!important;}" +
        ".vpd-footer{background:#0f2333!important;border-color:#23415a!important;color:#c7d9e6!important;}" +
        ".vpd-footer a{color:#c7d9e6!important;}.vpd-button{background:#7fd8f5!important;color:#041d30!important;}" +
        "}";

    public static string Render(FairDigestEmailView view, FairDigestEmailLinks links)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(links);
        var html = new StringBuilder(14_000);
        html.Append("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light dark\">")
            .Append("<meta name=\"supported-color-schemes\" content=\"light dark\">")
            .Append("<title>").Append(Encode(view.Subject)).Append("</title>")
            .Append("<link href=\"https://fonts.googleapis.com/css2?family=Newsreader:ital,opsz,wght@0,6..72,400;0,6..72,600;1,6..72,400&amp;family=Libre+Franklin:wght@400;600&amp;family=IBM+Plex+Mono:wght@400;500&amp;display=swap\" rel=\"stylesheet\">")
            .Append("<style>").Append(ResponsiveStyles).Append("</style></head>")
            .Append($"<body class=\"vpd-bg\" style=\"margin:0;padding:0;background:{Canvas};\">")
            .Append("<div data-zone=\"E8-texte-apercu\" style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:")
            .Append(Canvas).Append(";\">").Append(Encode(view.Preheader)).Append("</div>")
            .Append("<div data-zone=\"E8-objet\" style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;\">")
            .Append(Encode(view.Subject)).Append("</div>")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-bg\" style=\"background:{Canvas};\">")
            .Append("<tr><td align=\"center\" class=\"vpd-outer\" style=\"padding:32px 12px;\">")
            .Append($"<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-container\" style=\"width:600px;max-width:600px;background:{Paper};border:1px solid {Line};\">");

        AppendHeader(html, links);
        AppendHeadline(html, view);
        AppendFair(html, view);
        if (view.Message.Content.News.Count > 0)
        {
            AppendNewsSection(html, view.Message.Content.News, links);
        }

        if (view.Message.Content.Back.Count > 0)
        {
            AppendBackSection(html, view.Message.Content.Back, links);
        }

        if (view.TotalBookCount > 0)
        {
            AppendNoReservation(html, view.TotalBookCount);
            AppendSuggestionsButton(html, links);
        }

        AppendFooter(html, links);
        html.Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static void AppendHeader(StringBuilder html, FairDigestEmailLinks links)
    {
        html.Append("<tr><td class=\"vpd-pad\" data-zone=\"E8-entete\" style=\"padding:28px 40px 0;\">")
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        if (links.HttpsLogoUrl is { } logoUrl)
        {
            html.Append("<td style=\"padding:0 14px 0 0;vertical-align:middle;\">")
                .Append($"<img src=\"{Encode(logoUrl)}\" alt=\"{Encode(links.AssociationName)}\" width=\"52\" height=\"52\" style=\"display:block;width:52px;height:52px;border:0;background:{Paper};\"></td>");
        }

        html.Append("<td style=\"vertical-align:middle;\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:21px;font-weight:600;line-height:1.2;color:{InkSoft};\">{Encode(links.AssociationName)}</div>")
            .Append($"<div style=\"margin-top:3px;font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Blue};\">Bourse aux livres</div>")
            .Append("</td></tr></table>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin-top:24px;height:4px;\"><tr>")
            .Append($"<td style=\"height:4px;background:{Cyan};\"></td><td style=\"height:4px;background:#1497d6;\"></td><td style=\"height:4px;background:{Orange};\"></td>")
            .Append("</tr></table></td></tr>");
    }

    private static void AppendHeadline(StringBuilder html, FairDigestEmailView view)
    {
        var greeting = string.IsNullOrWhiteSpace(view.Message.FirstName)
            ? "Bonjour, voici ce qui vous attend samedi."
            : $"Bonjour {view.Message.FirstName.Trim()}, voici ce qui vous attend samedi.";
        html.Append("<tr><td class=\"vpd-pad\" data-zone=\"E8-accroche\" style=\"padding:32px 40px 8px;\">")
            .Append($"<p class=\"vpd-body\" style=\"margin:0 0 10px;font-family:{Sans};font-size:16px;line-height:1.5;color:{Body};\">{Encode(greeting)}</p>")
            .Append($"<h1 class=\"vpd-h1 vpd-ink\" {(view.Message.Content.News.Count == 0 ? "data-zone=\"E11-email-sans-nouveautes\"" : string.Empty)} style=\"margin:0;font-family:{Serif};font-size:32px;font-weight:400;line-height:1.2;color:{Ink};\">{Encode(view.Heading)}</h1>")
            .Append("</td></tr>");
    }

    private static void AppendFair(StringBuilder html, FairDigestEmailView view)
    {
        html.Append("<tr><td class=\"vpd-pad\" data-zone=\"E8-encart-bourse\" style=\"padding:20px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-panel\" style=\"background:{PaperBlue};border:1px solid {Line};border-top:4px solid {Orange};\"><tr><td style=\"padding:20px 24px 22px;\">")
            .Append($"<div style=\"font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Blue};\">Prochaine bourse</div>")
            .Append($"<div class=\"vpd-ink\" style=\"margin-top:6px;font-family:{Serif};font-size:25px;line-height:1.25;color:{Ink};\">À la bourse du <strong style=\"font-weight:600;\">{Encode(view.FullFairDate)}</strong></div>")
            .Append($"<div class=\"vpd-body\" style=\"margin-top:5px;font-family:{Sans};font-size:14px;color:{Body};\">{Encode(view.Message.Fair.Name)}</div>");
        if (view.FairHours is not null)
        {
            AppendFairDetail(html, "Horaires", view.FairHours);
        }

        if (!string.IsNullOrWhiteSpace(view.FairAddress))
        {
            AppendFairDetail(html, "Adresse", view.FairAddress);
        }

        html.Append("</td></tr></table></td></tr>");
    }

    private static void AppendFairDetail(StringBuilder html, string label, string value)
    {
        html.Append("<div style=\"padding-top:8px;\"><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>")
            .Append($"<td style=\"width:76px;padding:3px 12px 0 0;vertical-align:top;font-family:{Mono};font-size:11px;letter-spacing:0.1em;text-transform:uppercase;color:{Muted};\">{label}</td>")
            .Append($"<td class=\"vpd-body\" style=\"font-family:{Sans};font-size:15px;color:{Body};\">{Encode(value)}</td>")
            .Append("</tr></table></div>");
    }

    private static void AppendNewsSection(
        StringBuilder html,
        IReadOnlyList<PersonalRecommendation> news,
        FairDigestEmailLinks links)
    {
        html.Append("<tr><td class=\"vpd-pad\" data-zone=\"E8-section-nouveautes\" style=\"padding:30px 40px 0;\">")
            .Append($"<div style=\"font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Heart};\">Nouveautés pour vous</div>")
            .Append($"<div class=\"vpd-body\" style=\"margin-top:4px;font-family:{Sans};font-size:14px;color:{Muted};\">Arrivés depuis la dernière bourse, choisis d'après vos coups de cœur.</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
        foreach (var item in news)
        {
            AppendNewsCard(html, item, links);
        }

        html.Append("</table></td></tr>");
    }

    private static void AppendNewsCard(StringBuilder html, PersonalRecommendation recommendation, FairDigestEmailLinks links)
    {
        var book = recommendation.Book;
        var title = BookAlertEmailFormat.Title(book.Title);
        var authors = BookAlertEmailFormat.Authors(book.Authors);
        var edition = BookAlertEmailFormat.Edition(book.Publisher, book.PublicationYear, book.PhysicalFormat);
        var bookUrl = links.BookUrl(book);
        var cover = BookAlertEmailLinks.HttpsOrNull(book.CoverUrl);
        var hearts = Math.Clamp(recommendation.Hearts, 0, 5);
        html.Append("<tr data-zone=\"E8-carte-livre\"><td style=\"padding-top:14px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-card\" style=\"border:1px solid ")
            .Append(Line).Append(";\"><tr><td class=\"vpd-cover-cell\" width=\"92\" valign=\"top\" style=\"width:92px;padding:16px 0 16px 16px;\">");
        AppendCover(html, cover, title, 76, 114);
        html.Append("</td><td valign=\"top\" style=\"padding:16px 16px 16px 18px;\">")
            .Append($"<div data-zone=\"E8-coeurs\" style=\"font-family:{Sans};font-size:13px;color:#a3263e;\"><span style=\"letter-spacing:2px;font-size:16px;color:{Heart};\">{new string('♥', hearts)}{new string('♡', 5 - hearts)}</span> {hearts} cœurs sur 5</div>")
            .Append($"<div class=\"vpd-ink\" style=\"margin-top:4px;font-family:{Serif};font-size:20px;line-height:1.25;color:{Ink};\">{Encode(title)}</div>");
        if (authors is not null)
        {
            html.Append($"<div class=\"vpd-body\" style=\"margin-top:3px;font-family:{Sans};font-size:14px;color:{Body};\">{Encode(authors)}</div>");
        }

        if (edition is not null)
        {
            html.Append($"<div class=\"vpd-body\" style=\"margin-top:6px;font-family:{Mono};font-size:12px;color:{Muted};\">{Encode(edition)}</div>");
        }

        html.Append($"<div data-zone=\"E8-raison\" class=\"vpd-body\" style=\"margin-top:8px;font-family:{Sans};font-size:13.5px;color:{Body};\">{Encode(FairDigestEmailLayout.Reason(recommendation))}</div>");
        AppendBookLink(html, bookUrl);
        html.Append("</td></tr></table></td></tr>");
    }

    private static void AppendBackSection(
        StringBuilder html,
        IReadOnlyList<FairDigestBackItem> back,
        FairDigestEmailLinks links)
    {
        html.Append("<tr><td class=\"vpd-pad\" data-zone=\"E8-section-de-retour\" style=\"padding:30px 40px 0;\">")
            .Append($"<div style=\"font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Blue};\">De retour à la bourse</div>")
            .Append($"<div class=\"vpd-body\" style=\"margin-top:4px;font-family:{Sans};font-size:14px;color:{Muted};\">Des livres très demandés, de nouveau disponibles.</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
        foreach (var item in back)
        {
            AppendBackCard(html, item, links);
        }

        html.Append("</table></td></tr>");
    }

    private static void AppendBackCard(StringBuilder html, FairDigestBackItem item, FairDigestEmailLinks links)
    {
        var book = item.Book;
        var title = BookAlertEmailFormat.Title(book.Title);
        var authors = BookAlertEmailFormat.Authors(book.Authors);
        var bookUrl = links.BookUrl(book);
        var cover = BookAlertEmailLinks.HttpsOrNull(book.CoverUrl);
        html.Append("<tr><td data-zone=\"E8-carte-livre\" style=\"padding-top:12px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-card\" style=\"border:1px solid ")
            .Append(Line).Append(";\"><tr><td class=\"vpd-cover-cell\" width=\"68\" valign=\"middle\" style=\"width:68px;padding:14px 0 14px 14px;\">");
        AppendCover(html, cover, title, 52, 78);
        html.Append("</td><td valign=\"middle\" style=\"padding:14px 14px 14px 16px;\">")
            .Append($"<span data-zone=\"E8-etiquette-tres-demande\" style=\"display:inline-block;font-family:{Mono};font-size:10px;letter-spacing:0.12em;text-transform:uppercase;color:{Paper};background:{InkSoft};padding:3px 8px;\">{(item.IsVeryDemanded ? "Très demandé" : "De retour")}</span>")
            .Append($"<div class=\"vpd-ink\" style=\"margin-top:6px;font-family:{Serif};font-size:18px;color:{Ink};\">{Encode(title)}</div>");
        if (authors is not null)
        {
            html.Append($"<div class=\"vpd-body\" style=\"margin-top:2px;font-family:{Sans};font-size:14px;color:{Body};\">{Encode(authors)}");
            if (bookUrl is not null)
            {
                html.Append($" · <a data-zone=\"E8-lien-fiche\" href=\"{Encode(bookUrl)}\" style=\"font-weight:600;color:{Blue};\">Voir la fiche</a>");
            }

            html.Append("</div>");
        }
        else
        {
            AppendBookLink(html, bookUrl);
        }

        html.Append("</td></tr></table></td></tr>");
    }

    private static void AppendCover(StringBuilder html, string? coverUrl, string title, int width, int height)
    {
        if (coverUrl is not null)
        {
            html.Append($"<img src=\"{Encode(coverUrl)}\" alt=\"Couverture : {Encode(title)}\" width=\"{width}\" height=\"{height}\" class=\"vpd-cover\" style=\"display:block;width:{width}px;height:{height}px;border:1px solid {Line};object-fit:cover;background:{PaperBlue};\">");
            return;
        }

        html.Append($"<table role=\"presentation\" width=\"{width}\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-cover vpd-panel\" style=\"width:{width}px;height:{height}px;background:#f4efe3;border:1px solid #d9d2c0;\"><tr><td align=\"center\" valign=\"middle\" style=\"padding:8px;text-align:center;\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:11px;line-height:1.2;color:{InkSoft};\">{Encode(title)}</div>")
            .Append("</td></tr></table>");
    }

    private static void AppendBookLink(StringBuilder html, string? bookUrl)
    {
        if (bookUrl is not null)
        {
            html.Append($"<div style=\"margin-top:10px;\"><a data-zone=\"E8-lien-fiche\" href=\"{Encode(bookUrl)}\" style=\"font-family:{Sans};font-size:15px;font-weight:600;color:{Blue};\">Voir la fiche</a></div>");
        }
    }

    private static void AppendNoReservation(StringBuilder html, int totalCount)
    {
        html.Append("<tr><td class=\"vpd-pad\" data-zone=\"E8-non-reservation\" style=\"padding:26px 40px 0;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#fff4e8;border:1px solid #f6d2ab;\"><tr>")
            .Append("<td style=\"padding:14px 18px;font-family:").Append(Sans)
            .Append(";font-size:15px;line-height:1.5;color:#6b3305;\"><strong>")
            .Append(Encode(BookAlertEmailFormat.NoReservationLead(totalCount)))
            .Append("</strong> Premier arrivé, premier servi.</td></tr></table></td></tr>");
    }

    private static void AppendSuggestionsButton(StringBuilder html, FairDigestEmailLinks links)
    {
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:28px 40px 36px;\">")
            .Append($"<a data-zone=\"E8-bouton-suggestions\" href=\"{Encode(links.SuggestionsUrl)}\" class=\"vpd-button\" style=\"display:inline-block;background:{Blue};color:{Paper};font-family:{Sans};font-size:16px;font-weight:600;text-decoration:none;padding:14px 28px;border-radius:4px;\">Voir toutes mes suggestions</a>")
            .Append("</td></tr>");
    }

    private static void AppendFooter(StringBuilder html, FairDigestEmailLinks links)
    {
        html.Append("<tr><td class=\"vpd-pad vpd-footer\" data-zone=\"E8-pied\" style=\"background:")
            .Append(PaperSoft).Append(";border-top:1px solid ").Append(LineSoft)
            .Append(";padding:24px 40px 28px;font-family:").Append(Sans)
            .Append(";font-size:13px;line-height:1.6;color:").Append(Muted).Append(";\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:16px;color:{InkSoft};\">{Encode(links.AssociationName)} — Bourse aux livres</div>")
            .Append($"<div style=\"margin-top:4px;\"><a href=\"{Encode(links.WebsiteUrl)}\" style=\"color:{Blue};\">{Encode(links.WebsiteLabel)}</a></div>")
            .Append("<p data-zone=\"E8-raison-envoi\" style=\"margin:14px 0 0;\">Vous recevez cet e-mail parce que vous avez demandé une sélection avant chaque bourse.</p>");
        if (!string.IsNullOrWhiteSpace(links.UnsubscribeUrl))
        {
            html.Append($"<p style=\"margin:6px 0 0;\"><a data-zone=\"E8-desabonnement\" href=\"{Encode(links.UnsubscribeUrl)}\" style=\"display:inline-block;padding:6px 0;color:{Muted};\">Se désabonner</a></p>");
        }

        html.Append("</td></tr>");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
