namespace Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

public enum BookVerdict : byte
{
    Wanted,
    Selling,
    TooMany,
    FirstCopy
}

public readonly record struct BookVerdictFacts(
    int QuantityAvailable,
    int QuantityAnnounced,
    int SalesCount,
    int ActiveRequesterCount,
    bool IsRare);

public sealed record BookVerdictDecision(
    BookVerdict Verdict,
    bool IsRare,
    int TotalKnownQuantity,
    int SalesCount,
    int ActiveRequesterCount);

public static class BookVerdictCalculator
{
    public static BookVerdictDecision Calculate(
        BookVerdictFacts facts,
        int duplicateThreshold,
        int demandSalesThreshold)
    {
        ValidateNonNegative(facts.QuantityAvailable, nameof(facts.QuantityAvailable));
        ValidateNonNegative(facts.QuantityAnnounced, nameof(facts.QuantityAnnounced));
        ValidateNonNegative(facts.SalesCount, nameof(facts.SalesCount));
        ValidateNonNegative(facts.ActiveRequesterCount, nameof(facts.ActiveRequesterCount));
        ValidatePositive(duplicateThreshold, nameof(duplicateThreshold));
        ValidatePositive(demandSalesThreshold, nameof(demandSalesThreshold));

        var totalKnownQuantity = facts.QuantityAvailable + facts.QuantityAnnounced;
        var verdict = facts.ActiveRequesterCount > 0
            ? BookVerdict.Wanted
            : facts.SalesCount >= demandSalesThreshold
                ? BookVerdict.Selling
                : totalKnownQuantity >= duplicateThreshold
                    ? BookVerdict.TooMany
                    : BookVerdict.FirstCopy;

        return new BookVerdictDecision(
            verdict,
            facts.IsRare,
            totalKnownQuantity,
            facts.SalesCount,
            facts.ActiveRequesterCount);
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value cannot be negative.");
        }
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The threshold must be positive.");
        }
    }
}
