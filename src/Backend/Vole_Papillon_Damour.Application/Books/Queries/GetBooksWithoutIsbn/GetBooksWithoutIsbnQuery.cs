using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetBooksWithoutIsbn;

public sealed record GetBooksWithoutIsbnQuery(int Page, int PageSize, string? Search)
    : IRequest<ErrorOr<BookWithoutIsbnPageResult>>;