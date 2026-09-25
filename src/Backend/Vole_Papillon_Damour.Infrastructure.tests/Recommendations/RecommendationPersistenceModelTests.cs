using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.Recommendations;

public sealed class RecommendationPersistenceModelTests
{
    [Fact]
    public void Model_MapsRecommendationTablesKeysAndCascade()
    {
        using var context = new ProjectDbContext(new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options);

        var model = context.GetService<IDesignTimeModel>().Model;
        var profile = model.FindEntityType(typeof(BookSimilarityProfile));
        profile.Should().NotBeNull();
        profile!.GetTableName().Should().Be("BookSimilarityProfiles");
        profile.FindProperty(nameof(BookSimilarityProfile.Isbn13))!.GetColumnType().Should().Be("char(13)");
        profile.FindProperty(nameof(BookSimilarityProfile.Embedding))!.GetColumnType().Should().Be("varbinary(2048)");

        var neighbor = model.FindEntityType(typeof(BookNeighbor));
        neighbor!.FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().Equal(nameof(BookNeighbor.GenerationId), nameof(BookNeighbor.Isbn13), nameof(BookNeighbor.Rank));

        var generation = model.FindEntityType(typeof(RecommendationGeneration));
        generation!.GetSeedData().Single()[nameof(RecommendationGeneration.Id)].Should().Be((byte)1);
        generation.GetCheckConstraints().Should().ContainSingle(constraint => constraint.Sql == "[Id] = 1");

        var preference = model.FindEntityType(typeof(MemberRecommendationPreference));
        preference!.GetForeignKeys().Should().ContainSingle(foreignKey =>
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade && foreignKey.PrincipalEntityType.GetTableName() == "Users");
    }
}
