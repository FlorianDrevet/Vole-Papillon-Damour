using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BookMetadataEnrichmentBackgroundService(
    IBookMetadataEnrichmentQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BookMetadataEnrichmentBackgroundService> logger,
    IOptions<IsbndbOptions>? isbndbOptions = null) : BackgroundService
{
    private const int DefaultBulkSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<Isbn13> isbn13s;
            try
            {
                var max = Math.Clamp(isbndbOptions?.Value.BulkSize ?? DefaultBulkSize, 1, DefaultBulkSize);
                isbn13s = await queue.DequeueBatchAsync(max, stoppingToken);
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
                    new EnrichPendingBooksCommand(BatchSize: isbn13s.Count, Isbn13s: isbn13s),
                    stoppingToken);

                logger.LogInformation(
                    "Immediate book metadata enrichment completed for {IsbnCount} ISBNs. " +
                    "Processed: {Processed}, Resolved: {Resolved}, NotFound: {NotFound}, Failed: {Failed}",
                    isbn13s.Count,
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
                    "Immediate book metadata enrichment failed for {IsbnCount} ISBNs; the scheduled worker will retry them.",
                    isbn13s.Count);
            }
        }
    }
}
