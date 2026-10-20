using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

namespace Vole_Papillon_Damour.Infrastructure.tests.Recommendations;

public sealed class RecommendationOptionsTests
{
    [Fact]
    public void DefaultsToOneSimilarBookAndAllowsTwentyResults()
    {
        var options = new RecommendationOptions();

        options.SimilarMinCount.Should().Be(1);
        options.SimilarMaxCount.Should().Be(20);
    }
}
