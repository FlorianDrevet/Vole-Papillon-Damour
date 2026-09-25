using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookNeighborConfiguration : IEntityTypeConfiguration<BookNeighbor>
{
    public void Configure(EntityTypeBuilder<BookNeighbor> builder)
    {
        builder.ToTable("BookNeighbors");
        builder.HasKey(neighbor => new { neighbor.GenerationId, neighbor.Isbn13, neighbor.Rank });
        builder.Property(neighbor => neighbor.GenerationId).ValueGeneratedNever();
        builder.Property(neighbor => neighbor.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .IsRequired();
        builder.Property(neighbor => neighbor.Rank).ValueGeneratedNever();
        builder.Property(neighbor => neighbor.NeighborIsbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .IsRequired();
        builder.Property(neighbor => neighbor.Score).HasColumnType("real").IsRequired();
        builder.Property(neighbor => neighbor.Reason).HasConversion<byte>().IsRequired();
    }
}
