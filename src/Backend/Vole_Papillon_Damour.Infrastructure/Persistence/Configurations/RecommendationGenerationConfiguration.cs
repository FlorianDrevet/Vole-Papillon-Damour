using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class RecommendationGenerationConfiguration : IEntityTypeConfiguration<RecommendationGeneration>
{
    public void Configure(EntityTypeBuilder<RecommendationGeneration> builder)
    {
        builder.ToTable("RecommendationGenerations", table =>
            table.HasCheckConstraint("CK_RecommendationGenerations_Singleton", "[Id] = 1"));
        builder.HasKey(generation => generation.Id);
        builder.Property(generation => generation.Id).HasColumnType("tinyint").ValueGeneratedNever();
        builder.Property(generation => generation.CurrentGenerationId).HasColumnType("uniqueidentifier");
        builder.Property(generation => generation.ComputedAt).HasColumnType("datetime2");
        builder.Property(generation => generation.BookCount).IsRequired();
        builder.HasData(RecommendationGeneration.Singleton());
    }
}
