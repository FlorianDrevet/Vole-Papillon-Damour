using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record RetryBookRecommendationJobCommand(string Isbn13)
    : IRequest<ErrorOr<RetryBookRecommendationJobResult>>;

public sealed record RetryBookRecommendationJobResult(
    string Isbn13,
    string Status,
    int RequestVersion);
