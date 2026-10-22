using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.ProcessBookRecommendations;

public sealed record ProcessBookRecommendationsCommand(
    IReadOnlyCollection<BookRecommendationQueueItem> Books)
    : IRequest<ProcessBookRecommendationsResult>;

public sealed record ProcessBookRecommendationsResult(
    int Started,
    int Ready,
    int NoNotice,
    int Failed,
    int WaitingForMetadata);
