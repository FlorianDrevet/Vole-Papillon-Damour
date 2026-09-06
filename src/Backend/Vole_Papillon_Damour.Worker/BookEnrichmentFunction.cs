using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Commands.Background;

namespace Vole_Papillon_Damour.Worker;

public sealed class BookEnrichmentFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<BookEnrichmentFunction> logger)
{
    [Function("Enrich")]
    public async Task Run(
        [TimerTrigger("0 0 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(
            new EnrichPendingBooksCommand(),
            cancellationToken);

        logger.LogInformation(
            "Worker enrichment completed. Processed: {Processed}, Resolved: {Resolved}, " +
            "CoversUpdated: {CoversUpdated}, NotFound: {NotFound}, Failed: {Failed}",
            result.ProcessedCount,
            result.ResolvedCount,
            result.CoverUpdatedCount,
            result.NotFoundCount,
            result.FailedCount);
    }
}
