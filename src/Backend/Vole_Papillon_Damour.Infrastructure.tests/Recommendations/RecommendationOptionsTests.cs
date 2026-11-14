using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure;
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

    [Fact]
    public void AddInfrastructure_BindsConfiguredHeartThresholdsWithoutAppendingModelDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Recommendations:HeartThresholds:0"] = "0.20",
                ["Recommendations:HeartThresholds:1"] = "0.35",
                ["Recommendations:HeartThresholds:2"] = "0.50",
                ["Recommendations:HeartThresholds:3"] = "0.65",
                ["Recommendations:HeartThresholds:4"] = "0.80"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(
            configuration,
            runMigrations: false,
            registerAuthentication: false,
            registerMemberCardTokens: false);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<RecommendationOptions>>().Value.HeartThresholds
            .Should().Equal(0.20f, 0.35f, 0.50f, 0.65f, 0.80f);
    }

    [Fact]
    public void AddInfrastructure_UsesDefaultHeartThresholdsWhenConfigurationIsAbsent()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(
            configuration,
            runMigrations: false,
            registerAuthentication: false,
            registerMemberCardTokens: false);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<RecommendationOptions>>().Value.HeartThresholds
            .Should().Equal(0.50f, 0.58f, 0.66f, 0.74f, 0.82f);
    }
}
