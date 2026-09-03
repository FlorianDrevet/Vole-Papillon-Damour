using MediatR;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;
using Vole_Papillon_Damour.Contracts.Books.Responses;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class BookController
{
    public static IApplicationBuilder UseBookController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/books/{isbn13}/metadata",
                    async (
                        string isbn13,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!Isbn13.TryCreate(isbn13, out var normalizedIsbn13))
                        {
                            return DomainErrors.Book.InvalidIsbn(isbn13).Result();
                        }

                        var result = await mediator.Send(
                            new GetBookMetadataQuery(normalizedIsbn13),
                            cancellationToken);

                        return result.Match(
                            metadata => Results.Ok(new BookMetadataResponse(
                                metadata.Isbn13,
                                metadata.Title,
                                metadata.Authors,
                                metadata.Publisher,
                                metadata.PublicationYear,
                                metadata.CoverUrl,
                                metadata.Source,
                                metadata.WorkId,
                                metadata.RetrievedAt)),
                            error => error.Result());
                    })
                .WithName("GetBookMetadata")
                .AllowAnonymous();
        });
    }
}
