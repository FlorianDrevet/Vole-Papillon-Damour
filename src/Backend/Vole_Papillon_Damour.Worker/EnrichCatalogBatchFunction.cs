using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Worker;

public sealed class EnrichCatalogBatchFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<EnrichCatalogBatchFunction> logger)
{
    public const string FunctionName = "EnrichCatalogBatch";

    [Function(FunctionName)]
    public async Task<EnrichPendingBooksResult> Run(
        [ActivityTrigger] EnrichCatalogBatchRequest request,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(
            new EnrichPendingBooksCommand(
                BatchSize: request.BatchSize,
                IncludeMissingGenres: true,
                RefreshAllGenres: true,
                RefreshMissingBibliographicDetails: true,
                FullCatalogRefreshStartedAt: request.FullCatalogRefreshStartedAt),
            cancellationToken);

        logger.LogInformation(
            "Worker enrichment batch completed. Batch: {Batch}, Processed: {Processed}, " +
            "Resolved: {Resolved}, CoversUpdated: {CoversUpdated}, GenresUpdated: {GenresUpdated}, " +
            "BibliographicDetailsUpdated: {BibliographicDetailsUpdated}, NotFound: {NotFound}, Failed: {Failed}",
            request.BatchNumber,
            result.ProcessedCount,
            result.ResolvedCount,
            result.CoverUpdatedCount,
            result.GenreUpdatedCount,
            result.BibliographicDetailsUpdatedCount,
            result.NotFoundCount,
            result.FailedCount);

        return result;
    }
}
