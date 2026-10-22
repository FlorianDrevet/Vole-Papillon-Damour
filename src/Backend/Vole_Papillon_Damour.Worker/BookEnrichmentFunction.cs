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
}
