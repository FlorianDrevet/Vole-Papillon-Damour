using FluentValidation;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.WithdrawAnonymousFavorite;

public sealed class WithdrawAnonymousFavoriteCommandValidator : AbstractValidator<WithdrawAnonymousFavoriteCommand>
{
    public WithdrawAnonymousFavoriteCommandValidator()
    {
        RuleFor(command => command.Isbn13)
            .Must(value => Isbn13.TryCreate(value, out _))
            .WithErrorCode("Favorites.InvalidIsbn")
            .WithMessage("A valid ISBN-13 is required.");
    }
}
