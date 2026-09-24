using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetSimilarBooks;

public sealed class GetSimilarBooksQueryHandler(
    IProjectDbContext dbContext,
    IRecommendationSettings settings,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetSimilarBooksQuery, ErrorOr<IReadOnlyList<SimilarBookResult>>>
{
    public async Task<ErrorOr<IReadOnlyList<SimilarBookResult>>> Handle(
        GetSimilarBooksQuery query,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(query.Isbn13, out var isbn13))
        {
            return DomainErrors.Book.InvalidIsbn(query.Isbn13);
        }

        if (!settings.Enabled)
        {
            return Array.Empty<SimilarBookResult>();
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var generationId = await dbContext.RecommendationGenerations
            .AsNoTracking()
            .Where(generation => generation.Id == 1)
            .Select(generation => generation.CurrentGenerationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (generationId is null)
        {
            return Array.Empty<SimilarBookResult>();
        }

        var neighbors = await dbContext.BookNeighbors
            .AsNoTracking()
            .Where(neighbor =>
                neighbor.GenerationId == generationId.Value &&
                neighbor.Isbn13 == isbn13.Value &&
                neighbor.Score >= settings.SimilarMinScore)
            .OrderBy(neighbor => neighbor.Rank)
            .ToListAsync(cancellationToken);
        if (neighbors.Count == 0)
        {
            return Array.Empty<SimilarBookResult>();
        }

        var candidateIsbns = neighbors
            .Select(neighbor => Isbn13.TryCreate(neighbor.NeighborIsbn13, out var candidate)
                ? candidate
                : (Isbn13?)null)
            .Where(candidate => candidate.HasValue)
            .Select(candidate => candidate!.Value)
            .Distinct()
            .ToArray();
        if (candidateIsbns.Length == 0)
        {
            return Array.Empty<SimilarBookResult>();
        }

        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book =>
                candidateIsbns.Contains(book.Id) &&
                !book.IsHiddenFromCatalog &&
                book.RedirectedToIsbn13 == null)
            .ToListAsync(cancellationToken);
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => candidateIsbns.Contains(announcement.Isbn13))
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToReferencedFairListAsync(announcements, cancellationToken);
        var projectedBooks = PublicCatalogProjector.Project(
                books,
                announcements,
                fairs,
                new HashSet<string>(StringComparer.Ordinal),
                nowUtc)
            .ToDictionary(book => book.Isbn13, StringComparer.Ordinal);

        var results = new List<SimilarBookResult>(Math.Max(0, settings.SimilarMaxCount));
        var seenIsbns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var neighbor in neighbors)
        {
            if (!seenIsbns.Add(neighbor.NeighborIsbn13) ||
                !projectedBooks.TryGetValue(neighbor.NeighborIsbn13, out var book) ||
                (book.QuantityAvailable <= 0 && book.QuantityAnnounced <= 0))
            {
                continue;
            }

            results.Add(new SimilarBookResult(book, neighbor.Reason));
            if (results.Count >= settings.SimilarMaxCount)
            {
                break;
            }
        }

        return results.Count < settings.SimilarMinCount
            ? Array.Empty<SimilarBookResult>()
            : results;
    }
}
