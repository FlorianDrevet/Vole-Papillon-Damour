using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.ProcessBookRecommendations;
using Vole_Papillon_Damour.Application.Recommendations.Commands.SeedRecommendationsBackfillPage;

namespace Vole_Papillon_Damour.Worker;

public sealed class RecommendationsFunction(
    IServiceScopeFactory scopeFactory,
    IBookRecommendationWorkQueue workQueue,
    IRecommendationSettings settings,
    ILogger<RecommendationsFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("RecommendationsBackfill")]
    public async Task<HttpResponseData> StartBackfill(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recommendations/backfill")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            var disabled = request.CreateResponse(HttpStatusCode.Conflict);
            await disabled.WriteStringAsync(
                "Book recommendations are disabled. Enable Recommendations:Enabled before starting the backfill.",
                cancellationToken);
            return disabled;
        }

        var backfillId = Guid.NewGuid();
        await workQueue.EnqueueBackfillPageAsync(backfillId, null, cancellationToken);
        logger.LogInformation(
            "Manual book recommendation backfill queued. BackfillId: {BackfillId}",
            backfillId);

        var response = request.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(
            new
            {
                backfillId,
                status = "queued",
                message = "Progress and per-book errors are available in Back Office > Recommendations.",
            },
            cancellationToken);
        return response;
    }

    [Function("ProcessBookRecommendations")]
    public async Task ProcessQueueMessage(
        [QueueTrigger("book-recommendations", Connection = "AzureWebJobsStorage")] string messageJson,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<BookRecommendationQueueMessage>(messageJson, JsonOptions)
                      ?? throw new InvalidOperationException("Recommendation queue message is empty.");

        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        switch (message.Kind)
        {
            case BookRecommendationQueueMessageKind.BackfillPage:
                if (!settings.Enabled)
                {
                    logger.LogWarning(
                        "Recommendation backfill page skipped because recommendations are disabled. BackfillId: {BackfillId}",
                        message.BackfillId);
                    return;
                }

                var backfillId = message.BackfillId
                                 ?? throw new InvalidOperationException("A backfill queue message has no identifier.");
                var page = await sender.Send(
                    new SeedRecommendationsBackfillPageCommand(backfillId, message.Cursor),
                    cancellationToken);
                logger.LogInformation(
                    "Recommendation backfill page queued. BackfillId: {BackfillId}, Cursor: {Cursor}, Books: {Books}, JobsQueued: {JobsQueued}, Complete: {Complete}",
                    backfillId,
                    message.Cursor,
                    page.BooksScanned,
                    page.BooksQueued,
                    page.IsComplete);
                break;

            case BookRecommendationQueueMessageKind.ProcessBooks:
                var books = message.Books ?? [];
                var result = await sender.Send(
                    new ProcessBookRecommendationsCommand(books),
                    cancellationToken);
                logger.LogInformation(
                    "Recommendation batch completed. Started: {Started}, Ready: {Ready}, NoNotice: {NoNotice}, Failed: {Failed}, WaitingForMetadata: {WaitingForMetadata}",
                    result.Started,
                    result.Ready,
                    result.NoNotice,
                    result.Failed,
                    result.WaitingForMetadata);
                break;

            default:
                throw new InvalidOperationException($"Unknown recommendation message kind: {message.Kind}.");
        }
    }
}
