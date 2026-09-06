using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.SearchBibliographicReferences;
using Vole_Papillon_Damour.Contracts.Books.Responses;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class BibliographicReferenceController
{
    public static IApplicationBuilder UseBibliographicReferenceController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/catalog/reference/search",
                    async (
                        [FromQuery(Name = "q")] string? query,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new SearchBibliographicReferencesQuery(
                                query ?? string.Empty,
                                page ?? 1,
                                pageSize ?? 20),
                            cancellationToken);
                        return result.Match(
                            references => Results.Ok(new BookReferenceSearchResponse(
                                references.GeneratedAt,
                                references.Query,
                                references.Items.Select(item => new BookReferenceSearchItemResponse(
                                    item.Isbn13,
                                    item.WorkId,
                                    item.Title,
                                    item.Authors,
                                    item.Publisher,
                                    item.PublicationYear,
                                    item.CoverUrl,
                                    item.Source)).ToArray(),
                                references.Page,
                                references.PageSize)),
                            error => error.Result());
                    })
                .WithName("SearchExternalBibliographicReferences")
                .AllowAnonymous();
        });
    }
}
