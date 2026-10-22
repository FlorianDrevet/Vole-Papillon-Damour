using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class StartBookRecommendationsBackfillCommandHandler(
    IRecommendationSettings settings,
    IBookRecommendationWorkQueue workQueue)
    : IRequestHandler<StartBookRecommendationsBackfillCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(
        StartBookRecommendationsBackfillCommand request,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return Error.Conflict(
                "Recommendation.Disabled",
                "Les recommandations sont désactivées dans la configuration du service.");
        }

        var backfillId = Guid.NewGuid();
        await workQueue.EnqueueBackfillPageAsync(backfillId, null, cancellationToken);
        return backfillId;
    }
}
