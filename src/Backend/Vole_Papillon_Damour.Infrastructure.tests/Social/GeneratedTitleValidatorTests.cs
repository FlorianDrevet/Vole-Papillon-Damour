using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.Ai;

namespace Vole_Papillon_Damour.Infrastructure.tests.Social;

public sealed class GeneratedTitleValidatorTests
{
    [Fact]
    public void NormalizeAndValidate_AcceptsAFrenchTitleInTheAllowedRange()
    {
        var title = GeneratedTitleValidator.NormalizeAndValidate(
            "La fête du livre revient dans notre commune");

        title.Should().Be("La fête du livre revient dans notre commune");
    }

    [Theory]
    [InlineData("Trop court")]
    [InlineData("Un titre qui contient un hashtag #association")]
    [InlineData("Un titre avec un point final.")]
    [InlineData("Un titre avec un point d'interrogation ?")]
    [InlineData("A wonderful community event with the association")]
    public void NormalizeAndValidate_RejectsTitlesThatBreakTheEditorialContract(string title)
    {
        GeneratedTitleValidator.NormalizeAndValidate(title).Should().BeNull();
    }

    [Fact]
    public void NormalizeAndValidate_RejectsLineBreaksAndEmoji()
    {
        GeneratedTitleValidator.NormalizeAndValidate("Une actualité joyeuse\nà partager")
            .Should().BeNull();
        GeneratedTitleValidator.NormalizeAndValidate("Une actualité joyeuse 🎉 à partager")
            .Should().BeNull();
    }

    [Fact]
    public void NormalizeAndValidate_RemovesWrappingQuotesAndVerbosePrefixes()
    {
        var title = GeneratedTitleValidator.NormalizeAndValidate(
            "Proposition : « La fête du livre revient »");

        title.Should().Be("La fête du livre revient");
    }

    [Fact]
    public void NormalizeAndValidate_RejectsTitlesLongerThanSeventyCharacters()
    {
        var title = new string('a', 71);

        GeneratedTitleValidator.NormalizeAndValidate(title).Should().BeNull();
    }
}
