using FluentValidation;

namespace Vole_Papillon_Damour.Application.Favorites.Queries.GetMyFavorites;

public sealed class GetMyFavoritesQueryValidator : AbstractValidator<GetMyFavoritesQuery>
{
    public GetMyFavoritesQueryValidator()
    {
        RuleFor(query => query.ExternalId).NotEmpty();
        RuleFor(query => query.Email).NotEmpty().MaximumLength(320);
    }
}
