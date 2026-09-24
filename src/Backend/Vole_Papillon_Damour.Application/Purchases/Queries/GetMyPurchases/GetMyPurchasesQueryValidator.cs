using FluentValidation;

namespace Vole_Papillon_Damour.Application.Purchases.Queries.GetMyPurchases;

public sealed class GetMyPurchasesQueryValidator : AbstractValidator<GetMyPurchasesQuery>
{
    public GetMyPurchasesQueryValidator()
    {
        RuleFor(query => query.ExternalId)
            .NotEmpty();

        RuleFor(query => query.Email)
            .NotEmpty();

        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 10);

        RuleFor(query => query.Cursor)
            .Must(cursor => string.IsNullOrWhiteSpace(cursor) ||
                (cursor.Length <= 128 && cursor.All(character =>
                    char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
            .WithMessage("Le curseur d’historique n’est pas valide.");
    }
}
