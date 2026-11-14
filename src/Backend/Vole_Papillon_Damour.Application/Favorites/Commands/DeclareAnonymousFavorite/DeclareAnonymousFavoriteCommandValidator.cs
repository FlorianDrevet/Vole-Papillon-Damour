using FluentValidation;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.DeclareAnonymousFavorite;

public sealed class DeclareAnonymousFavoriteCommandValidator : AbstractValidator<DeclareAnonymousFavoriteCommand>
{
    public DeclareAnonymousFavoriteCommandValidator()
    {
        RuleFor(command => command.Isbn13)
            .Must(value => Isbn13.TryCreate(value, out _))
            .WithErrorCode("Favorites.InvalidIsbn")
            .WithMessage("A valid ISBN-13 is required.");
    }
}
