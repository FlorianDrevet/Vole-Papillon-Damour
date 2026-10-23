using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendCatchUpAsync(cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Manual ISBNdb catch-up failed unexpectedly.");
            throw;
        }
    }

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
