using System.Diagnostics;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetDeadStock;

public sealed class GetDeadStockQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<GetDeadStockQueryHandler> logger)
    : IRequestHandler<GetDeadStockQuery, ErrorOr<DeadStockResult>>
{
    private const int MaxDateRangeMonths = 120_000;

    public async Task<ErrorOr<DeadStockResult>> Handle(
        GetDeadStockQuery query,
        CancellationToken cancellationToken)
    {
        if (query.MinAgeMonths <= 0)
        {
            return Error.Validation(
                "Books.InvalidDeadStockAge",
                "The minimum dead-stock age must be positive, in months.");
        }

        if (query.MinAgeMonths > MaxDateRangeMonths)
        {
            return Error.Validation(
                "Books.InvalidDeadStockAge",
                $"The minimum dead-stock age must be between 1 and {MaxDateRangeMonths} months.");
        }

        if (query.MinQuantity < 0)
        {
            return Error.Validation(
                "Books.InvalidDeadStockQuantity",
                "The minimum dead-stock quantity cannot be negative.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Books.InvalidClock",
                "The book catalog clock must be expressed in UTC.");
        }

        var startedAt = Stopwatch.GetTimestamp();
        DateTime cutoff;
        try
        {
            cutoff = generatedAt.AddMonths(-query.MinAgeMonths);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Error.Validation(
                "Books.InvalidDeadStockAge",
                "The minimum dead-stock age does not fit in the supported date range.");
        }

        // Availability is derived from the append-only movement ledger instead
        // of trusting FirstSeenAt: a fiche can first be seen after a rejection,
        // while only a positive stock movement makes it available. A positive
        // correction is included because a manual inventory correction can be
        // the first moment at which a legacy fiche becomes available.
        var candidatesQuery = dbContext.Books
            .AsNoTracking()
            .Where(book =>
                book.RedirectedToIsbn13 == null &&
                book.QuantityAvailable > query.MinQuantity &&
                book.SalesCount == 0)
            .Where(book => !dbContext.BookMovements
                .AsNoTracking()
                .Any(movement =>
                    movement.Isbn13 == book.Id &&
                    movement.Type == BookMovementType.Sale))
            .Select(book => new
            {
                Isbn13 = book.Id,
                book.Title,
                book.Authors,
                book.Publisher,
                book.PublicationYear,
                book.Genre,
                book.QuantityAvailable,
                FirstAvailableAt = dbContext.BookMovements
                    .AsNoTracking()
                    .Where(movement =>
                        movement.Isbn13 == book.Id &&
                        movement.Quantity > 0 &&
                        (movement.Type == BookMovementType.DirectEntry ||
                         movement.Type == BookMovementType.FairRelease ||
                         movement.Type == BookMovementType.Correction))
                    .Min(movement => movement.OccurredAt)
            })
            .Where(candidate => candidate.FirstAvailableAt <= cutoff)
            .OrderByDescending(candidate => candidate.QuantityAvailable)
            .ThenBy(candidate => candidate.FirstAvailableAt)
            .ThenBy(candidate => candidate.Isbn13);

        var candidateRows = await candidatesQuery
            .ToListAsync(cancellationToken);
        var candidates = candidateRows
            .Select(candidate => new DeadStockBookResult(
                candidate.Isbn13.Value,
                candidate.Title,
                candidate.Authors,
                candidate.Publisher,
                candidate.PublicationYear,
                candidate.Genre,
                candidate.QuantityAvailable,
                candidate.FirstAvailableAt))
            .ToArray();

        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        logger?.LogInformation(
            $"Book dead-stock query completed. MinAgeMonths: {query.MinAgeMonths}, " +
            $"MinQuantity: {query.MinQuantity}, CandidateCount: {candidates.Length}, " +
            $"DurationMs: {elapsedMilliseconds}");

        return new DeadStockResult(
            generatedAt,
            query.MinAgeMonths,
            query.MinQuantity,
            candidates);
    }
}
