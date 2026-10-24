using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Isbndb.Queries.SearchIsbndbEditions;

public sealed record SearchIsbndbEditionsQuery(string? Query)
    : IRequest<ErrorOr<SearchIsbndbEditionsResult>>;

public sealed record SearchIsbndbEditionsResult(
    IReadOnlyList<SearchIsbndbEditionResult> Items,
    int Total,
    bool Truncated,
    string? Unavailable);

public sealed record SearchIsbndbEditionResult(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? CoverUrl,
    string? Binding);
