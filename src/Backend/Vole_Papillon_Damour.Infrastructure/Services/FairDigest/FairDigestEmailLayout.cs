using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

public sealed record FairDigestEmailView(
    FairDigestEmailMessage Message,
    string Subject,
    string Preheader,
    string Heading,
    string FullFairDate,
    string? FairHours,
    string? FairAddress,
    int TotalBookCount);

public static class FairDigestEmailLayout
{
    public static string Reason(PersonalRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        var main = recommendation.Reason == NeighborReason.NextTome
            ? $"La suite de {recommendation.SeedTitle}"
            : $"Parce que vous avez aimé {recommendation.SeedTitle}";
        if (recommendation.OtherContributorCount <= 0)
        {
            return main;
        }

        var otherWord = recommendation.OtherContributorCount == 1 ? "autre" : "autres";
        return $"{main} · et {recommendation.OtherContributorCount} {otherWord}";
    }

    public static FairDigestEmailView Build(FairDigestEmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Content);
        ArgumentNullException.ThrowIfNull(message.Fair);

        var newsCount = message.Content.News.Count;
        var backCount = message.Content.Back.Count;
        var totalCount = newsCount + backCount;
        var fairDate = BookAlertEmailFormat.ShortDate(message.Fair.DateStart);
        var subject = newsCount > 0
            ? $"Avant la bourse du {fairDate} : {CountLabel(totalCount, "livre")} qui {(totalCount == 1 ? "pourrait" : "pourraient")} vous plaire"
            : $"Avant la bourse du {fairDate} : {CountLabel(backCount, "livre")} de retour";
        var preheader = string.Join(" · ", new[]
        {
            newsCount == 0 ? null : $"{CountWord(newsCount, "nouveauté")} pour vous",
            backCount == 0 ? null : $"{CountLabel(backCount, "livre")} de retour"
        }.Where(part => part is not null));
        var heading = newsCount > 0
            ? $"{CountLabel(totalCount, "livre")} qui {(totalCount == 1 ? "pourrait" : "pourraient")} vous plaire"
            : $"{backCount} {(backCount == 1 ? "livre très demandé est" : "livres très demandés sont")} de retour";

        return new FairDigestEmailView(
            message,
            subject,
            preheader,
            heading,
            BookAlertEmailFormat.Date(message.Fair.DateStart),
            BookAlertEmailFormat.Hours(message.Fair.DateStart, message.Fair.DateEnd),
            message.Fair.Address,
            totalCount);
    }

    private static string CountLabel(int count, string noun) =>
        $"{count} {noun}{(count == 1 ? string.Empty : "s")}";

    private static string CountWord(int count, string word) =>
        $"{count} {word}{(count == 1 ? string.Empty : "s")}";

}
