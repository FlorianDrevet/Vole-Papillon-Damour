using FluentValidation;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetSuggestionsForFavorites;

public sealed class GetSuggestionsForFavoritesQueryValidator : AbstractValidator<GetSuggestionsForFavoritesQuery>
{
    public GetSuggestionsForFavoritesQueryValidator()
    {
        RuleFor(query => query.Isbn13s)
            .NotEmpty()
            .Must(isbns => isbns.Count <= 50);

        RuleForEach(query => query.Isbn13s)
            .Must(isbn => Isbn13.TryCreate(isbn, out var parsed) && parsed.Value == isbn)
            .WithMessage("Each favorite must be a canonical ISBN-13.");

        RuleFor(query => query.MinimumHearts)
            .InclusiveBetween(1, 4);

        RuleFor(query => query.Isbn13s)
            .Must(isbns => isbns.Distinct(StringComparer.Ordinal).Count() == isbns.Count)
            .WithMessage("Favorite ISBNs must be unique.");
    }
}
