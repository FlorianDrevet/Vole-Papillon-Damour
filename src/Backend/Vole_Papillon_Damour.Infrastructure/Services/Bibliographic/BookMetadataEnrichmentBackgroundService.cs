using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BookMetadataEnrichmentBackgroundService(
    IBookMetadataEnrichmentQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BookMetadataEnrichmentBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Isbn13 isbn13;
            try
            {
                isbn13 = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(
                    new EnrichPendingBooksCommand(Isbn13: isbn13),
                    stoppingToken);

                logger.LogInformation(
                    "Immediate book metadata enrichment completed for ISBN {Isbn13}. " +
                    "Processed: {Processed}, Resolved: {Resolved}, NotFound: {NotFound}, Failed: {Failed}",
                    isbn13.Value,
                    result.ProcessedCount,
                    result.ResolvedCount,
                    result.NotFoundCount,
                    result.FailedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Immediate book metadata enrichment failed for ISBN {Isbn13}; the scheduled worker will retry it.",
                    isbn13.Value);
            }
        }
    }
}
