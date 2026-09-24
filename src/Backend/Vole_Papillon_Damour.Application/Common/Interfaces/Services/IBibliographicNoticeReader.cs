using Vole_Papillon_Damour.Application.Recommendations.Similarity;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBibliographicNoticeReader
{
    Task<SimilarityEdition?> ReadAsync(string isbn13, CancellationToken cancellationToken);
}
