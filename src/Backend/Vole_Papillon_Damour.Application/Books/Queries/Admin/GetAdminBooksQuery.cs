using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record GetAdminBooksQuery(
    string? Search,
    string? MetadataStatus,
    bool? Rare,
    bool? Hidden,
    int Page = 1,
    int PageSize = 50,
    bool? Undated = null) : IRequest<ErrorOr<AdminBookPageResult>>;
