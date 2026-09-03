using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;

public sealed class GetBookMetadataQueryHandler(IBibliographicMetadataResolver resolver)
    : IRequestHandler<GetBookMetadataQuery, ErrorOr<BookMetadataResult>>
{
    public async Task<ErrorOr<BookMetadataResult>> Handle(
        GetBookMetadataQuery query,
        CancellationToken cancellationToken)
    {
        var metadata = await resolver.ResolveAsync(query.Isbn13, cancellationToken);

        if (metadata is not null)
        {
            return metadata;
        }

        return Errors.Book.MetadataNotFound(query.Isbn13.Value);
    }
}
