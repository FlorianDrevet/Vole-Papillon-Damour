using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public sealed record BookNeighborRow(
    string Isbn13,
    byte Rank,
    string NeighborIsbn13,
    float Score,
    NeighborReason Reason);

public interface IBookNeighborWriter
{
    /// <summary>Writes a complete generation, then atomically switches the current generation.</summary>
    Task WriteGenerationAsync(
        Guid generationId,
        IReadOnlyCollection<BookNeighborRow> rows,
        int bookCount,
        DateTime computedAt,
        CancellationToken cancellationToken);

    /// <summary>Atomically replaces changed neighbor lists and returns the active generation identifier.</summary>
    Task<Guid> WriteIncrementalGenerationAsync(
        Guid generationId,
        IReadOnlyCollection<string> replacedIsbns,
        IReadOnlyCollection<BookNeighborRow> replacementRows,
        int bookCount,
        DateTime computedAt,
        CancellationToken cancellationToken);
}
