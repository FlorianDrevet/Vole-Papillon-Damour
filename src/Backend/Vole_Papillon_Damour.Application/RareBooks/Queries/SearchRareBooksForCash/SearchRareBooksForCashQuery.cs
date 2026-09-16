using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.SearchRareBooksForCash;

public sealed record SearchRareBooksForCashQuery(
    string? Search,
    int Page = 1,
    int PageSize = 25) : IRequest<ErrorOr<RareBookCashSearchResult>>;
