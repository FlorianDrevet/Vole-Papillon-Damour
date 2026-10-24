using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbAbsentBooks;

public sealed record GetIsbndbAbsentBooksQuery(string? Status = null, string? Search = null, int Page = 1, int PageSize = 50)
    : IRequest<ErrorOr<IsbndbAbsentBooksPageResult>>;

public sealed record IsbndbAbsentBooksPageResult(
    IReadOnlyList<IsbndbAbsentBookResult> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record IsbndbAbsentBookResult(
    string Isbn13,
    string? Title,
    string? Authors,
    string? CoverUrl,
    string? FallbackSource,
    int QuantityAvailable,
    IReadOnlyList<string> MissingFields,
    string? Summary,
    string? Genre,
    string? Publisher,
    int? PublicationYear,
    int? Pages,
    string? Edition,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Subjects,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? IsbndbCheckedAt,
    DateTimeOffset? IsbndbCompletedAt);
