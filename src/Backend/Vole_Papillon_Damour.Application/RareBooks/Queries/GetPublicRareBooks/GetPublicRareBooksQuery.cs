using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBooks;

public sealed record GetPublicRareBooksQuery(
    string? Shelf = null,
    bool IncludeSold = true,
    RareBookSortOrder Sort = RareBookSortOrder.Recent,
    int Page = 1,
    int PageSize = 24) : IRequest<ErrorOr<PublicRareBookPageResult>>;
