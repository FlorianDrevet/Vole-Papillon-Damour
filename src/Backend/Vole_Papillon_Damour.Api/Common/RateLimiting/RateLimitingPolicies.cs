namespace Vole_Papillon_Damour.Api.Common.RateLimiting;

/// <summary>
/// Names of the per-client rate limiting policies applied to the anonymous
/// endpoints that proxy to external bibliographic providers (BnF, Open
/// Library, Google Books). Keeping the names here avoids typos between
/// registration in <see cref="DependencyInjection"/> and each endpoint's
/// <c>RequireRateLimiting</c> call.
/// </summary>
public static class RateLimitingPolicies
{
    public const string BibliographicMetadata = "BibliographicMetadata";
    public const string BibliographicSearch = "BibliographicSearch";
}
