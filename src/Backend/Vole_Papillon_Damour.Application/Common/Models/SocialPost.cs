namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record SocialPost(
    string ExternalId,
    string? Caption,
    Uri Permalink,
    DateTimeOffset PublishedAt,
    IReadOnlyList<SocialPostMedia> Medias);
