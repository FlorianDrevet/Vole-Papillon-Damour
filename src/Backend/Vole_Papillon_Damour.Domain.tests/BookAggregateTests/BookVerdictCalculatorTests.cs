using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests;

public sealed class BookVerdictCalculatorTests
{
    [Fact]
    public void Calculate_WhenBookIsWanted_PrioritizesRequesterSignal()
    {
        var facts = new BookVerdictFacts(4, 2, 12, 2, IsRare: true);

        var decision = BookVerdictCalculator.Calculate(facts, duplicateThreshold: 5, demandSalesThreshold: 1);

        decision.Verdict.Should().Be(BookVerdict.Wanted);
        decision.IsRare.Should().BeTrue();
    }

    [Fact]
    public void Calculate_WhenBookHasSalesButNoRequester_PrioritizesSalesSignal()
    {
        var facts = new BookVerdictFacts(4, 2, 1, 0, IsRare: false);

        var decision = BookVerdictCalculator.Calculate(facts, duplicateThreshold: 5, demandSalesThreshold: 1);

        decision.Verdict.Should().Be(BookVerdict.Selling);
    }

    [Fact]
    public void Calculate_WhenNoHigherSignalAndDuplicateThresholdIsReached_ReturnsTooMany()
    {
        var facts = new BookVerdictFacts(3, 2, 0, 0, IsRare: false);

        var decision = BookVerdictCalculator.Calculate(facts, duplicateThreshold: 5, demandSalesThreshold: 1);

        decision.Verdict.Should().Be(BookVerdict.TooMany);
    }

    [Fact]
    public void Calculate_WhenNoKnownQuantityOrSales_ReturnsFirstCopy()
    {
        var facts = new BookVerdictFacts(0, 0, 0, 0, IsRare: false);

        var decision = BookVerdictCalculator.Calculate(facts, duplicateThreshold: 5, demandSalesThreshold: 1);

        decision.Verdict.Should().Be(BookVerdict.FirstCopy);
    }
}
