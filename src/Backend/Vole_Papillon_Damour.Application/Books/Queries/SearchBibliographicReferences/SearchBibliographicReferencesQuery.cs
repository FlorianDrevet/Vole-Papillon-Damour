using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.SearchBibliographicReferences;

public sealed record SearchBibliographicReferencesQuery(
    string Query,
    int Page = 1,
    int PageSize = 20) : IRequest<ErrorOr<BookReferenceSearchResult>>;
