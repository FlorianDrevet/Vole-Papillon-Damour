using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBooks;

public sealed record GetAdminRareBooksQuery(
    string? Search = null,
    string? Status = null,
    RareBookAdminAvailability Availability = RareBookAdminAvailability.All,
    bool? HasIsbn = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool WithoutPhoto = false,
    int Page = 1,
    int PageSize = 50) : IRequest<ErrorOr<RareBookPageResult>>;
