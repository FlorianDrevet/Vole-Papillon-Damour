using FluentValidation;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.MergeMyFavorites;

public sealed class MergeMyFavoritesCommandValidator : AbstractValidator<MergeMyFavoritesCommand>
{
    public MergeMyFavoritesCommandValidator()
    {
        RuleFor(command => command.ExternalId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().MaximumLength(320);
        RuleFor(command => command.Entries).NotNull().Must(entries => entries.Count <= 200);
        RuleForEach(command => command.Entries).ChildRules(entry =>
        {
            entry.RuleFor(item => item.Isbn13)
                .Must(value => Isbn13.TryCreate(value, out _))
                .WithErrorCode("Favorites.InvalidIsbn")
                .WithMessage("A valid ISBN-13 is required.");
        });
    }
}
