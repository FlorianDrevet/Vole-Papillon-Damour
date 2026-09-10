namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record SocialPostMedia(
    SocialMediaKind Kind,
    Uri? ContentUrl,
    Uri? ThumbnailUrl);
