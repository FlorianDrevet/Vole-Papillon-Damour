using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetSuggestionsForFavorites;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class GetSuggestionsForFavoritesQueryValidatorTests
{
    [Fact]
    public void Validate_AllowsUpToFiftyUniqueCanonicalIsbns()
    {
        var validator = new GetSuggestionsForFavoritesQueryValidator();
        var isbns = Enumerable.Range(0, 50).Select(CreateValidIsbn).ToArray();

        var result = validator.Validate(new GetSuggestionsForFavoritesQuery(isbns, 3));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsMoreThanFiftyIsbnsAndDuplicateValues()
    {
        var validator = new GetSuggestionsForFavoritesQueryValidator();
        var fiftyOne = Enumerable.Range(0, 51).Select(CreateValidIsbn).ToArray();
        var duplicate = new[] { "9782070408504", "9782070408504" };

        validator.Validate(new GetSuggestionsForFavoritesQuery(fiftyOne, 3)).IsValid.Should().BeFalse();
        validator.Validate(new GetSuggestionsForFavoritesQuery(duplicate, 3)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Validate_RejectsMinimumHeartsOutsideAvailableValues(int minimumHearts)
    {
        var validator = new GetSuggestionsForFavoritesQueryValidator();

        validator.Validate(new GetSuggestionsForFavoritesQuery(["9782070408504"], minimumHearts))
            .IsValid.Should().BeFalse();
    }

    private static string CreateValidIsbn(int index)
    {
        var body = $"97800000{index:D4}";
        var sum = body.Select((digit, position) =>
            (digit - '0') * (position % 2 == 0 ? 1 : 3)).Sum();
        return body + ((10 - (sum % 10)) % 10);
    }
}
