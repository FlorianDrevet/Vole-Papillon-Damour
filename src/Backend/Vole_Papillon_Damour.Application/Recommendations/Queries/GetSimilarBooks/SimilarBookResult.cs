using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetSimilarBooks;

public sealed record SimilarBookResult(PublicCatalogBookResult Book, NeighborReason Reason);
