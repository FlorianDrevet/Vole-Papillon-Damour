using FluentAssertions;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;

namespace Vole_Papillon_Damour.Domain.tests.MemberCards;

public sealed class MemberCardRecoveryCodeTests
{
    [Theory]
    [InlineData("LUNE-4271", "LUNE-4271")]
    [InlineData("lune-4271", "LUNE-4271")]
    [InlineData(" lune 4271 ", "LUNE-4271")]
    [InlineData("LUNE4271", "LUNE-4271")]
    [InlineData("lune--4271", "LUNE-4271")]
    public void TryNormalize_AcceptsCommonTypingVariants(string input, string expected)
    {
        MemberCardRecoveryCode.TryNormalize(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LUNE-42")]
    [InlineData("LUNE-42712")]
    [InlineData("1234-LUNE")]
    [InlineData("LUNÉ-4271")]
    [InlineData("VPDC1.abc.def")]
    public void TryNormalize_RejectsOtherShapes(string? input)
    {
        MemberCardRecoveryCode.TryNormalize(input, out _).Should().BeFalse();
    }

    [Fact]
    public void Generate_ProducesANormalisedCode()
    {
        var code = MemberCardRecoveryCode.Generate(max => max - 1);

        MemberCardRecoveryCode.TryNormalize(code, out var normalized).Should().BeTrue();
        normalized.Should().Be(code);
    }

    [Fact]
    public void Words_AreUniqueUppercaseAsciiWithoutAmbiguousLetters()
    {
        MemberCardRecoveryCode.Words.Should().OnlyHaveUniqueItems().And.HaveCount(64);
        MemberCardRecoveryCode.Words.Should().OnlyContain(word => word.All(c => c >= 'A' && c <= 'Z'));
    }
}
