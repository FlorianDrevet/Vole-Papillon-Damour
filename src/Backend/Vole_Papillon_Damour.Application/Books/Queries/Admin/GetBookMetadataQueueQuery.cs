using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record GetBookMetadataQueueQuery(int Page = 1, int PageSize = 100)
    : IRequest<ErrorOr<BookMetadataQueuePageResult>>;
