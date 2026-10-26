using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Isbndb.Commands.CatchUpIsbndbBooks;
using Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

namespace Vole_Papillon_Damour.Worker;

public sealed class IsbndbCatchUpFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<IsbndbCatchUpFunction> logger,
    IOptions<IsbndbOptions> isbndbOptions)
{
    public const string ManualActivityFunctionName = "IsbndbCatchUpManualActivity";
    private readonly IsbndbOptions _options = isbndbOptions.Value;

    [Function("IsbndbCatchUp")]
    public async Task Run(
        [TimerTrigger("%Isbndb:CatchUpSchedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendCatchUpAsync(cancellationToken);
            logger.LogInformation(
                "ISBNdb catch-up completed. Filled: {Filled}, Absent: {Absent}, " +
                "Rechecked: {Rechecked}, Remaining: {Remaining}, StopReason: {StopReason}",
                result.Filled,
                result.Absent,
                result.Rechecked,
                result.Remaining,
                result.StopReason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "ISBNdb catch-up failed unexpectedly.");
            throw;
        }
    }

    [Function("IsbndbCatchUpManual")]
    public async Task<HttpResponseData> RunManually(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient)
    {
        try
        {
            var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                IsbndbCatchUpOrchestrationFunction.FunctionName);
            logger.LogInformation(
                "Manual ISBNdb catch-up orchestration started. InstanceId: {InstanceId}",
                instanceId);
            return await durableClient.CreateCheckStatusResponseAsync(request, instanceId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Manual ISBNdb catch-up could not be scheduled.");
            throw;
        }
    }

    [Function(ManualActivityFunctionName)]
    public async Task<CatchUpIsbndbBooksResult> RunManuallyAsActivity(
        [ActivityTrigger] string _,
        CancellationToken cancellationToken) => await SendCatchUpAsync(cancellationToken);

    private async Task<CatchUpIsbndbBooksResult> SendCatchUpAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var command = new CatchUpIsbndbBooksCommand(
            Math.Clamp(_options.BulkSize, 1, 100),
            Math.Max(0, _options.AbsentRecheckDays));
        return await sender.Send(command, cancellationToken);
    }
}
