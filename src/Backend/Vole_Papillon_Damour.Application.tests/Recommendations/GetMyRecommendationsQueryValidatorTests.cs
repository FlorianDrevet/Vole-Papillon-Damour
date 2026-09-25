using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class GetMyRecommendationsQueryValidatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void Validate_BoundsLimitToConfiguredPersonalMaximum(int limit, bool expectedValid)
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.PersonalMaxCount.Returns(8);
        var validator = new GetMyRecommendationsQueryValidator(settings);

        var result = validator.Validate(new GetMyRecommendationsQuery(
            "00000000-0000-0000-0000-000000000001",
            "member@example.test",
            null,
            null,
            limit));

        result.IsValid.Should().Be(expectedValid);
    }
}
