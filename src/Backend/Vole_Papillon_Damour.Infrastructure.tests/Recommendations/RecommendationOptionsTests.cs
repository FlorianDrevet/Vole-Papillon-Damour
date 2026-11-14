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
        options.HeartThresholds.Should().Equal(0.50f, 0.58f, 0.66f, 0.74f, 0.82f);
        options.DefaultMinimumHearts.Should().Be(3);
    }
}
