using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.DurableTask.Client;

namespace Vole_Papillon_Damour.Worker;

public sealed class BookEnrichmentFunction(ILogger<BookEnrichmentFunction> logger)
{
    [Function("Enrich")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient)
    {
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            EnrichCatalogOrchestrationFunction.FunctionName);
        logger.LogInformation(
            "Manual book enrichment orchestration started. InstanceId: {InstanceId}",
            instanceId);

        return await durableClient.CreateCheckStatusResponseAsync(request, instanceId);
    }

    [Function("ResumeEnrich")]
    public async Task<HttpResponseData> Resume(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "Enrich/resume")] HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient)
    {
        EnrichCatalogResumeRequest? resumeRequest;
        try
        {
            resumeRequest = await request.ReadFromJsonAsync<EnrichCatalogResumeRequest>();
        }
        catch (JsonException)
        {
            return await CreateResumeRequestErrorAsync(request);
        }

        if (resumeRequest is null ||
            resumeRequest.FullCatalogRefreshStartedAt == default ||
            resumeRequest.FullCatalogRefreshStartedAt > DateTimeOffset.UtcNow ||
            resumeRequest.LastCompletedBatchNumber < 0)
        {
            return await CreateResumeRequestErrorAsync(request);
        }

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            EnrichCatalogOrchestrationFunction.FunctionName,
            resumeRequest);
        logger.LogInformation(
            "Manual book enrichment resumed after batch {LastCompletedBatchNumber} from {RefreshStartedAt}. " +
            "InstanceId: {InstanceId}",
            resumeRequest.LastCompletedBatchNumber,
            resumeRequest.FullCatalogRefreshStartedAt,
            instanceId);

        return await durableClient.CreateCheckStatusResponseAsync(request, instanceId);
    }

    private static async Task<HttpResponseData> CreateResumeRequestErrorAsync(HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteStringAsync(
            "Provide FullCatalogRefreshStartedAt and a non-negative LastCompletedBatchNumber as JSON.");
        return response;
    }
}
