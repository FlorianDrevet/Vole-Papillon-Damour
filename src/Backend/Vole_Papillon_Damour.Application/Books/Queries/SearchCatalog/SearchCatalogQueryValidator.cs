using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;

public sealed class SearchCatalogQueryValidator : AbstractValidator<SearchCatalogQuery>
{
    private const int MaxSearchLength = 100;

    public SearchCatalogQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(MaxSearchLength)
            .WithMessage($"The search text must be at most {MaxSearchLength} characters.");
    }
}
