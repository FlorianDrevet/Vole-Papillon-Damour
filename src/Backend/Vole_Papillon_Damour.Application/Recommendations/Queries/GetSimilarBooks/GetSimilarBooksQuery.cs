using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetSimilarBooks;

public sealed record GetSimilarBooksQuery(string Isbn13)
    : IRequest<ErrorOr<IReadOnlyList<SimilarBookResult>>>;
