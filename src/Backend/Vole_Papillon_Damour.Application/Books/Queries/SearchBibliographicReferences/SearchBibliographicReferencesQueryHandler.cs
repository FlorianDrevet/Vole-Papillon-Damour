using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Books.Queries.SearchBibliographicReferences;

public sealed class SearchBibliographicReferencesQueryHandler(
    IBibliographicSearchService searchService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SearchBibliographicReferencesQuery, ErrorOr<BookReferenceSearchResult>>
{
    public async Task<ErrorOr<BookReferenceSearchResult>> Handle(
        SearchBibliographicReferencesQuery query,
        CancellationToken cancellationToken)
    {
        var search = query.Query?.Trim();
        if (string.IsNullOrWhiteSpace(search) || search.Length > 200)
        {
            return Error.Validation(
                "Book.InvalidReferenceSearch",
                "The bibliographic search must contain between 2 and 200 characters.");
        }

        if (search.Length < 2 || query.Page <= 0 || query.PageSize is <= 0 or > 50)
        {
            return Error.Validation(
                "Book.InvalidReferenceSearch",
                "The bibliographic search parameters are invalid.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The bibliographic search clock must be expressed in UTC.");
        }

        IReadOnlyList<BookReferenceSearchItem> items;
        try
        {
            items = await searchService.SearchAsync(
                search,
                query.Page,
                query.PageSize,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Error.Failure(
                "Book.ReferenceSearchUnavailable",
                "The external bibliographic reference is temporarily unavailable.");
        }

        return new BookReferenceSearchResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            search,
            items,
            query.Page,
            query.PageSize);
    }
}
