using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IIsbndbClient
{
    Task<IsbndbLookup> GetBookAsync(
        Isbn13 isbn13,
        IsbndbUsage usage,
        CancellationToken cancellationToken);

    Task<IsbndbBulkLookup> GetBooksAsync(
        IReadOnlyList<Isbn13> isbn13s,
        IsbndbUsage usage,
        CancellationToken cancellationToken);

    Task<IsbndbSearch> SearchAsync(
        string text,
        IsbndbUsage usage,
        CancellationToken cancellationToken);
}

public abstract record IsbndbLookup
{
    public sealed record Found(IsbndbBook Book) : IsbndbLookup;
    public sealed record NotFound : IsbndbLookup;
    public sealed record QuotaExhausted(DateTime? ResetsAt) : IsbndbLookup;
    public sealed record Unavailable(string Reason) : IsbndbLookup;
}

public enum IsbndbBulkLookupStatus : byte
{
    Success,
    QuotaExhausted,
    Unavailable
}

public sealed record IsbndbBulkLookup(
    IReadOnlyDictionary<Isbn13, IsbndbBook> Found,
    IReadOnlyList<Isbn13> Missing,
    IsbndbBulkLookupStatus Status = IsbndbBulkLookupStatus.Success,
    DateTime? ResetsAt = null,
    string? Reason = null);

public enum IsbndbSearchStatus : byte
{
    Success,
    QuotaExhausted,
    Unavailable
}

public sealed record IsbndbSearch(
    IReadOnlyList<IsbndbBook> Books,
    int Total,
    IsbndbSearchStatus Status = IsbndbSearchStatus.Success,
    DateTime? ResetsAt = null,
    string? Reason = null);
