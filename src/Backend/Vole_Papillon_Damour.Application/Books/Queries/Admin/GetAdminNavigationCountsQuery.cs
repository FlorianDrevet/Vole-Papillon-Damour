using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record GetAdminNavigationCountsQuery(bool IsAdministrator, bool IsRareBooksOnly)
    : IRequest<ErrorOr<AdminNavigationCountsResult>>;
