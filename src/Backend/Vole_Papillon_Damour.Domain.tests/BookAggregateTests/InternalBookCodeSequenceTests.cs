using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests;

public sealed class InternalBookCodeSequenceTests
{
    [Theory]
    [InlineData(1L, "2000000000015")]
    [InlineData(2L, "2000000000022")]
    public void CreateInternalFromSequence_BuildsAValidEan13WithInternalPrefix(long number, string expected)
    {
        var code = BookCode.CreateInternalFromSequence(number);

        code.IsInternal.Should().BeTrue();
        code.Value.Should().Be(expected);
        BookCode.IsValidInternalCode(code.Value).Should().BeTrue();
        code.Value[0].Should().Be('2');
        code.Value.Should().NotStartWith("978").And.NotStartWith("979");
    }

    [Fact]
    public void CreateInternalFromSequence_RejectsNumbersOutOfRange()
    {
        var zero = () => BookCode.CreateInternalFromSequence(0);
        var tooLarge = () => BookCode.CreateInternalFromSequence(BookCode.MaximumInternalSequenceNumber + 1);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        tooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IssueNext_NeverRepeatsACodeAndCountsEveryIssue()
    {
        var sequence = InternalBookCodeSequence.Create();

        var first = sequence.IssueNext();
        var second = sequence.IssueNext();
        var third = sequence.IssueNext();

        new[] { first, second, third }.Distinct().Should().HaveCount(3);
        sequence.LastIssuedNumber.Should().Be(3);
        first.Should().Be(BookCode.CreateInternalFromSequence(1));
        third.Should().Be(BookCode.CreateInternalFromSequence(3));
    }
}