namespace Vole_Papillon_Damour.Contracts.Recommendations;

public sealed record RecommendationPreferenceRequest(
    bool Enabled,
    int? MinimumHearts = null,
    bool? FairDigestConsent = null);

public sealed record RecommendationPreferenceResponse(
    bool Enabled,
    int MinimumHearts,
    DateTimeOffset? FairDigestConsentedAt);
