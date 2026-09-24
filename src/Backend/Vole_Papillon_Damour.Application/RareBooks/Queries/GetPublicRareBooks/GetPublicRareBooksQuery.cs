using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBooks;

public sealed record GetPublicRareBooksQuery(
    bool IncludeSold = true,
    RareBookSortOrder Sort = RareBookSortOrder.PriceDescending,
    int Page = 1,
    int PageSize = 24,
    string? Search = null) : IRequest<ErrorOr<PublicRareBookPageResult>>;
