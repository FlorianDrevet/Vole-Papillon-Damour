using FluentValidation;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.AddMyFavorite;

public sealed class AddMyFavoriteCommandValidator : AbstractValidator<AddMyFavoriteCommand>
{
    public AddMyFavoriteCommandValidator()
    {
        RuleFor(command => command.ExternalId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().MaximumLength(320);
        RuleFor(command => command.Isbn13)
            .Must(value => Isbn13.TryCreate(value, out _))
            .WithErrorCode("Favorites.InvalidIsbn")
            .WithMessage("A valid ISBN-13 is required.");
    }
}
