using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Worker;

public static class EnrichCatalogOrchestrationFunction
{
    public const string FunctionName = "EnrichCatalogOrchestration";
    private const int BatchSize = 50;

    [Function(FunctionName)]
    public static async Task<EnrichPendingBooksResult> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var resumeRequest = context.GetInput<EnrichCatalogResumeRequest>();
        var startedAt = context.CurrentUtcDateTime;
        var batchNumber = 0;
        if (resumeRequest is not null)
        {
            if (resumeRequest.FullCatalogRefreshStartedAt == default ||
                resumeRequest.FullCatalogRefreshStartedAt.UtcDateTime > startedAt ||
                resumeRequest.LastCompletedBatchNumber < 0)
            {
                throw new ArgumentException("The enrichment resume request is invalid.", nameof(resumeRequest));
            }

            startedAt = resumeRequest.FullCatalogRefreshStartedAt.UtcDateTime;
            batchNumber = resumeRequest.LastCompletedBatchNumber;
        }

        var totals = new EnrichPendingBooksResult(0, 0, 0, 0);

        while (true)
        {
            batchNumber++;
            var batch = await context.CallActivityAsync<EnrichPendingBooksResult>(
                EnrichCatalogBatchFunction.FunctionName,
                new EnrichCatalogBatchRequest(batchNumber, BatchSize, startedAt));
            totals = Add(totals, batch);
            context.SetCustomStatus(new
            {
                batchNumber,
                totals.ProcessedCount,
                totals.ResolvedCount,
                totals.NotFoundCount,
                totals.FailedCount,
                totals.CoverUpdatedCount,
                totals.GenreUpdatedCount,
                totals.BibliographicDetailsUpdatedCount
            });

            if (batch.ProcessedCount < BatchSize)
            {
                return totals;
            }

            await context.CreateTimer(
                context.CurrentUtcDateTime.AddSeconds(1),
                CancellationToken.None);
        }
    }

    private static EnrichPendingBooksResult Add(
        EnrichPendingBooksResult totals,
        EnrichPendingBooksResult batch) =>
        new(
            totals.ProcessedCount + batch.ProcessedCount,
            totals.ResolvedCount + batch.ResolvedCount,
            totals.NotFoundCount + batch.NotFoundCount,
            totals.FailedCount + batch.FailedCount,
            totals.CoverUpdatedCount + batch.CoverUpdatedCount,
            totals.GenreUpdatedCount + batch.GenreUpdatedCount,
            totals.BibliographicDetailsUpdatedCount + batch.BibliographicDetailsUpdatedCount);
}
