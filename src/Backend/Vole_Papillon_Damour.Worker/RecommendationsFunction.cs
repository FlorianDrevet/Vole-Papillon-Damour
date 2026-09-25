using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;

namespace Vole_Papillon_Damour.Worker;

public sealed class RecommendationsFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<RecommendationsFunction> logger)
{
    [Function("Recommendations")]
    public async Task Run(
        [TimerTrigger("0 30 3 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var profiles = await sender.Send(new RefreshBookSimilarityProfilesCommand(), cancellationToken);
        logger.LogInformation(
            "Worker recommendation profile refresh completed. Candidates: {Candidates}, Found: {Found}, NotFound: {NotFound}",
            profiles.Candidates,
            profiles.Found,
            profiles.NotFound);

        var neighbors = await sender.Send(new RecomputeBookNeighborsCommand(), cancellationToken);
        logger.LogInformation(
            "Worker recommendation computation completed. Books: {Books}, Embedded: {Embedded}, Neighbors: {Neighbors}",
            neighbors.Books,
            neighbors.Embedded,
            neighbors.Neighbors);
    }
}
