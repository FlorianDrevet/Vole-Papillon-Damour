using System.Net;
using System.Text;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>
/// HTML de l'alerte, compatible clients mail : tableaux de présentation, styles en ligne,
/// 600 px, une colonne. Maquettes : docs/features/book-alert-email-template/maquettes/.
/// </summary>
public static class BookAlertEmailHtml
{
    private const string Serif = "Newsreader,Georgia,'Times New Roman',serif";
    private const string Sans = "'Libre Franklin',Arial,Helvetica,sans-serif";
    private const string Mono = "'IBM Plex Mono','Courier New',monospace";

    private const string Canvas = "#e6edf2";
    private const string Paper = "#ffffff";
    private const string PaperSoft = "#f7fbfe";
    private const string PaperBlue = "#e9f4fb";
    private const string Ink = "#041d30";
    private const string InkSoft = "#072b45";
    private const string Body = "#33546b";
    private const string Muted = "#4a6478";
    private const string Blue = "#0c6ea6";
    private const string BlueBright = "#1497d6";
    private const string Cyan = "#7fd8f5";
    private const string Orange = "#f0801c";
    private const string Line = "#d9e9f4";
    private const string LineSoft = "#e2eef7";
    private const string NoticeBackground = "#fff4e8";
    private const string NoticeBorder = "#f6d2ab";
    private const string NoticeInk = "#6b3305";

    private const string ResponsiveAndDarkStyles =
        "body{margin:0;padding:0;}" +
        "@media (max-width:620px){" +
        ".vpd-outer{padding:0!important;}" +
        ".vpd-container{width:100%!important;border:0!important;}" +
        ".vpd-pad{padding-left:20px!important;padding-right:20px!important;}" +
        ".vpd-h1{font-size:28px!important;}" +
        ".vpd-main{font-size:22px!important;}" +
        ".vpd-cover-cell{width:72px!important;}" +
        ".vpd-cover{width:72px!important;height:108px!important;}" +
        ".vpd-label,.vpd-value{display:block!important;width:auto!important;}" +
        ".vpd-button{display:block!important;text-align:center!important;}" +
        "}" +
        "@media (prefers-color-scheme:dark){" +
        ".vpd-bg{background:#081520!important;}" +
        ".vpd-container{background:#0b1b28!important;border-color:#23415a!important;}" +
        ".vpd-ink{color:#ffffff!important;}" +
        ".vpd-body{color:#c7d9e6!important;}" +
        ".vpd-accent{color:#7fd8f5!important;}" +
        ".vpd-panel{background:#12304a!important;border-color:#23415a!important;}" +
        ".vpd-card{border-color:#23415a!important;}" +
        ".vpd-chip{background:#12304a!important;border-color:#23415a!important;color:#e3eef6!important;}" +
        ".vpd-notice{background:#3a2410!important;border-color:#5a3a1c!important;color:#ffd7ad!important;}" +
        ".vpd-button{background:#7fd8f5!important;color:#041d30!important;}" +
        ".vpd-footer{background:#0f2333!important;border-color:#23415a!important;color:#c7d9e6!important;}" +
        ".vpd-footer a{color:#c7d9e6!important;}" +
        ".vpd-logo{background:#ffffff!important;}" +
        "}";

    public static string Render(BookAlertEmailView view, BookAlertEmailLinks links, string subject)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(links);
        var html = new StringBuilder(16_384);
        html.Append("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light dark\">")
            .Append("<meta name=\"supported-color-schemes\" content=\"light dark\">")
            .Append("<title>").Append(Encode(subject)).Append("</title>")
            .Append("<link href=\"https://fonts.googleapis.com/css2?family=Newsreader:ital,opsz,wght@0,6..72,400;0,6..72,600;1,6..72,400&amp;family=Libre+Franklin:wght@400;600&amp;family=IBM+Plex+Mono:wght@400;500&amp;display=swap\" rel=\"stylesheet\">")
            .Append("<style>").Append(ResponsiveAndDarkStyles).Append("</style></head>")
            .Append($"<body class=\"vpd-bg\" style=\"margin:0;padding:0;background:{Canvas};\">")
            .Append("<div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:")
            .Append(Canvas).Append(";\">").Append(Encode(view.Preheader)).Append("</div>")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-bg\" style=\"background:{Canvas};\">")
            .Append("<tr><td align=\"center\" class=\"vpd-outer\" style=\"padding:32px 12px;\">")
            .Append($"<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-container\" style=\"width:600px;max-width:600px;background:{Paper};border:1px solid {Line};\">");

        AppendHeader(html, links);
        AppendHeadline(html, view);
        for (var index = 0; index < view.Groups.Count; index++)
        {
            AppendGroup(html, view.Groups[index], links, index == 0);
        }

        AppendRareItems(html, view);
        if (view.BookCount > 0)
        {
            AppendNoReservation(html, view.BookCount);
            AppendSearchListButton(html, links);
        }
        else
        {
            html.Append("<tr><td style=\"padding:0 0 36px;font-size:0;line-height:0;\">&#160;</td></tr>");
        }

        AppendFooter(html, view, links);
        html.Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static void AppendHeader(StringBuilder html, BookAlertEmailLinks links)
    {
        var association = Encode(links.AssociationName);
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:28px 40px 0;\">")
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        if (links.HttpsLogoUrl is { } logoUrl)
        {
            html.Append("<td style=\"padding:0 14px 0 0;vertical-align:middle;\">")
                .Append($"<img src=\"{Encode(logoUrl)}\" alt=\"{association}\" width=\"52\" height=\"52\" class=\"vpd-logo\" ")
                .Append($"style=\"display:block;width:52px;height:52px;border:0;background:{Paper};\"></td>");
        }

        html.Append("<td style=\"vertical-align:middle;\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:21px;font-weight:600;line-height:1.2;color:{InkSoft};\">{association}</div>")
            .Append($"<div class=\"vpd-accent\" style=\"margin-top:3px;font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Blue};\">Bourse aux livres</div>")
            .Append("</td></tr></table>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin-top:24px;\"><tr>");
        foreach (var color in new[] { Cyan, BlueBright, Orange })
        {
            html.Append($"<td height=\"4\" style=\"height:4px;background:{color};font-size:0;line-height:0;\">&#160;</td>");
        }

        html.Append("</tr></table></td></tr>");
    }

    private static void AppendHeadline(StringBuilder html, BookAlertEmailView view)
    {
        var greeting = view.FirstName is null ? "Bonjour," : $"Bonjour {view.FirstName},";
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:32px 40px 8px;\">")
            .Append($"<p class=\"vpd-ink\" style=\"margin:0 0 10px;font-family:{Sans};font-size:16px;line-height:1.5;color:{InkSoft};\">{Encode(greeting)}</p>")
            .Append($"<h1 class=\"vpd-h1 vpd-ink\" style=\"margin:0;font-family:{Serif};font-size:32px;font-weight:400;line-height:1.2;color:{Ink};\">");
        if (view.Headline.EmphasizedTitle is { } title)
        {
            html.Append("<em>").Append(Encode(title)).Append("</em>");
        }

        html.Append(Encode(view.Headline.Text)).Append("</h1></td></tr>");
    }

    private static void AppendGroup(
        StringBuilder html,
        BookAlertEmailGroup group,
        BookAlertEmailLinks links,
        bool isFirst)
    {
        var accent = group.Mode == ScanMode.AvailableNow ? BlueBright : Orange;
        html.Append($"<tr><td class=\"vpd-pad\" style=\"padding:{(isFirst ? 20 : 28)}px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-panel\" style=\"background:{PaperBlue};border:1px solid {Line};border-top:4px solid {accent};\">")
            .Append("<tr><td style=\"padding:20px 24px 22px;\">")
            .Append($"<div class=\"vpd-accent\" style=\"font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Blue};\">{Encode(group.Label)}</div>")
            .Append($"<div class=\"vpd-main vpd-ink\" style=\"margin-top:6px;font-family:{Serif};font-size:25px;line-height:1.25;color:{Ink};\">")
            .Append(Encode(group.MainLinePrefix))
            .Append($"<strong style=\"font-weight:600;\">{Encode(group.MainLineEmphasis)}</strong></div>");
        if (group.NextOpening is { } nextOpening)
        {
            html.Append($"<div class=\"vpd-ink\" style=\"margin-top:6px;font-family:{Sans};font-size:16px;line-height:1.4;color:{InkSoft};\">{Encode(nextOpening)}</div>");
        }

        if (group.Hours is not null || group.Address is not null)
        {
            html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin-top:16px;\">");
            AppendDetail(html, "Horaires", group.Hours);
            AppendDetail(html, "Adresse", group.Address);
            html.Append("</table>");
        }

        html.Append("</td></tr></table></td></tr>");
        for (var index = 0; index < group.Items.Count; index++)
        {
            AppendBookCard(html, group.Items[index], links, index == 0);
        }
    }

    private static void AppendDetail(StringBuilder html, string label, string? value)
    {
        if (value is null)
        {
            return;
        }

        html.Append("<tr>")
            .Append($"<td class=\"vpd-label vpd-body\" width=\"88\" valign=\"top\" style=\"width:88px;padding:4px 0;font-family:{Mono};font-size:11px;letter-spacing:0.1em;text-transform:uppercase;color:{Muted};\">{label}</td>")
            .Append($"<td class=\"vpd-value vpd-ink\" valign=\"top\" style=\"padding:2px 0 4px;font-family:{Sans};font-size:15px;line-height:1.4;color:{InkSoft};\">{Encode(value)}</td>")
            .Append("</tr>");
    }

    private static void AppendBookCard(
        StringBuilder html,
        BookAlertOutboxItem item,
        BookAlertEmailLinks links,
        bool isFirst)
    {
        var title = BookAlertEmailFormat.Title(item);
        html.Append($"<tr><td class=\"vpd-pad\" style=\"padding:{(isFirst ? 16 : 12)}px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-card\" style=\"border:1px solid {Line};\"><tr>")
            .Append("<td class=\"vpd-cover-cell\" width=\"88\" valign=\"top\" style=\"width:88px;padding:18px 0 18px 18px;\">");
        AppendCover(html, item, title, links);
        html.Append("</td><td valign=\"top\" style=\"padding:18px 18px 18px 20px;\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:21px;line-height:1.25;color:{Ink};\">{Encode(title)}</div>");
        if (BookAlertEmailFormat.Authors(item) is { } authors)
        {
            html.Append($"<div class=\"vpd-body\" style=\"margin-top:4px;font-family:{Sans};font-size:15px;color:{Body};\">{Encode(authors)}</div>");
        }

        if (BookAlertEmailFormat.Edition(item) is { } edition)
        {
            html.Append($"<div class=\"vpd-body\" style=\"margin-top:10px;font-family:{Mono};font-size:12px;letter-spacing:0.04em;color:{Muted};\">{Encode(edition)}</div>");
        }

        html.Append($"<div style=\"margin-top:10px;\"><span class=\"vpd-chip\" style=\"display:inline-block;font-family:{Sans};font-size:13px;color:{InkSoft};background:{PaperSoft};border:1px solid {Line};padding:3px 10px;\">{BookAlertEmailFormat.Quantity(item.Quantity)}</span></div>");
        if (links.BookUrl(item.PublicPath) is { } bookUrl)
        {
            html.Append($"<div style=\"margin-top:14px;\"><a href=\"{Encode(bookUrl)}\" class=\"vpd-accent\" style=\"display:inline-block;padding:6px 0;font-family:{Sans};font-size:15px;font-weight:600;color:{Blue};\">Voir la fiche</a></div>");
        }

        html.Append("</td></tr></table></td></tr>");
    }

    private static void AppendCover(
        StringBuilder html,
        BookAlertOutboxItem item,
        string title,
        BookAlertEmailLinks links)
    {
        if (BookAlertEmailLinks.HttpsOrNull(item.CoverUrl) is { } coverUrl)
        {
            html.Append($"<img src=\"{Encode(coverUrl)}\" alt=\"{Encode(title)}\" width=\"88\" height=\"132\" class=\"vpd-cover\" ")
                .Append($"style=\"display:block;width:88px;height:132px;border:1px solid {Line};object-fit:cover;background:{PaperBlue};\">");
            return;
        }

        html.Append($"<table role=\"presentation\" width=\"88\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-cover vpd-panel\" style=\"width:88px;height:132px;background:{PaperBlue};border:1px solid {Line};\">")
            .Append("<tr><td align=\"center\" valign=\"middle\" style=\"padding:12px 8px;text-align:center;\">");
        if (links.HttpsLogoUrl is { } logoUrl)
        {
            html.Append($"<img src=\"{Encode(logoUrl)}\" alt=\"\" width=\"40\" height=\"40\" style=\"display:block;width:40px;height:40px;margin:0 auto 10px;border:0;\">");
        }

        html.Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:11px;line-height:1.2;color:{InkSoft};\">{Encode(title)}</div>")
            .Append("</td></tr></table>");
    }

    private static void AppendRareItems(StringBuilder html, BookAlertEmailView view)
    {
        if (view.RareItems.Count == 0)
        {
            return;
        }

        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:28px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td style=\"border-top:1px solid {Line};padding-top:20px;\">")
            .Append($"<div class=\"vpd-body\" style=\"font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Muted};\">Livres rares suivis</div>")
            .Append($"<ul class=\"vpd-ink\" style=\"margin:10px 0 0;padding:0 0 0 18px;font-family:{Sans};font-size:15px;line-height:1.6;color:{InkSoft};\">");
        foreach (var rare in view.RareItems)
        {
            var author = string.IsNullOrWhiteSpace(rare.AuthorMention)
                ? string.Empty
                : $" — {Encode(rare.AuthorMention.Trim())}";
            html.Append($"<li><em>{Encode(rare.Title)}</em>{author} : n'est plus disponible.</li>");
        }

        html.Append("</ul></td></tr></table></td></tr>");
    }

    private static void AppendNoReservation(StringBuilder html, int bookCount)
    {
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:24px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-notice\" style=\"background:{NoticeBackground};border:1px solid {NoticeBorder};\">")
            .Append($"<tr><td class=\"vpd-notice\" style=\"padding:14px 18px;font-family:{Sans};font-size:15px;line-height:1.5;color:{NoticeInk};\">")
            .Append($"<strong>{Encode(BookAlertEmailFormat.NoReservationLead(bookCount))}</strong> Premier arrivé, premier servi.")
            .Append("</td></tr></table></td></tr>");
    }

    private static void AppendSearchListButton(StringBuilder html, BookAlertEmailLinks links)
    {
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:28px 40px 36px;\">")
            .Append($"<a href=\"{Encode(links.SearchListUrl)}\" class=\"vpd-button\" style=\"display:inline-block;background:{Blue};color:#ffffff;font-family:{Sans};font-size:16px;font-weight:600;text-decoration:none;padding:14px 28px;border-radius:4px;\">Voir ma liste de recherche</a>")
            .Append("</td></tr>");
    }

    private static void AppendFooter(StringBuilder html, BookAlertEmailView view, BookAlertEmailLinks links)
    {
        html.Append($"<tr><td class=\"vpd-pad vpd-footer\" style=\"background:{PaperSoft};border-top:1px solid {LineSoft};padding:24px 40px 28px;font-family:{Sans};font-size:13px;line-height:1.6;color:{Muted};\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:16px;color:{InkSoft};\">{Encode(links.AssociationName)} — Bourse aux livres</div>")
            .Append($"<div style=\"margin-top:4px;\"><a href=\"{Encode(links.WebsiteUrl)}\" class=\"vpd-accent\" style=\"color:{Blue};\">{Encode(links.WebsiteLabel)}</a></div>")
            .Append($"<p style=\"margin:14px 0 0;\">{Encode(BookAlertEmailFormat.Reason(view.MentionedCount))}</p>");
        if (links.Unsubscribe is { } unsubscribe)
        {
            html.Append($"<p style=\"margin:6px 0 0;\"><a href=\"{Encode(unsubscribe)}\" style=\"display:inline-block;padding:6px 0;color:{Muted};\">Se désabonner</a></p>");
        }

        html.Append("</td></tr>");
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
