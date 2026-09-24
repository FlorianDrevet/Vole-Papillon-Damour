using System.Text.RegularExpressions;
using FluentValidation;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Queries.LookupCheckoutPassage;

public sealed class LookupCheckoutPassageQueryValidator : AbstractValidator<LookupCheckoutPassageQuery>
{
    private static readonly Regex ShortReference = new("^[A-Fa-f0-9]{8}$", RegexOptions.Compiled);

    public LookupCheckoutPassageQueryValidator()
    {
        RuleFor(query => query.Reference)
            .Must(IsSupportedReference)
            .WithMessage("Indiquez une référence de huit caractères ou l’identifiant complet du passage.");
    }

    private static bool IsSupportedReference(string? reference)
    {
        var value = reference?.Trim();
        return !string.IsNullOrEmpty(value) &&
            (Guid.TryParse(value, out _) || ShortReference.IsMatch(value));
    }
}
