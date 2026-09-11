using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface ISocialFeedClient
{
    Task<IReadOnlyList<SocialPost>> GetRecentPostsAsync(CancellationToken cancellationToken);
}
