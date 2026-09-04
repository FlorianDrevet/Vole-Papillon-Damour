using FluentValidation;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Queries.GetMyWatchlist;

public sealed class GetMyWatchlistQueryValidator : AbstractValidator<GetMyWatchlistQuery>
{
    public GetMyWatchlistQueryValidator()
    {
        RuleFor(query => query.ExternalId).NotEmpty();
        RuleFor(query => query.Email)
            .NotEmpty()
            .MaximumLength(320);
    }
}
