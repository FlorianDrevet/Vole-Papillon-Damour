using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookSimilarityProfileConfiguration : IEntityTypeConfiguration<BookSimilarityProfile>
{
    public void Configure(EntityTypeBuilder<BookSimilarityProfile> builder)
    {
        builder.ToTable("BookSimilarityProfiles");
        builder.Ignore(profile => profile.Id);
        builder.HasKey(profile => profile.Isbn13);
        builder.Property(profile => profile.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .ValueGeneratedNever();
        builder.Property(profile => profile.NoticeJson).HasColumnType("nvarchar(max)");
        builder.Property(profile => profile.NoticeFound).IsRequired();
        builder.Property(profile => profile.NoticeFetchedAt).HasColumnType("datetime2");
        builder.Property(profile => profile.ProfileText).HasColumnType("nvarchar(max)");
        builder.Property(profile => profile.ProfileTextHash)
            .HasColumnType("char(64)")
            .IsUnicode(false);
        builder.Property(profile => profile.Embedding).HasColumnType("varbinary(2048)");
        builder.Property(profile => profile.EmbeddedTextHash)
            .HasColumnType("char(64)")
            .IsUnicode(false);
        builder.Property(profile => profile.EmbeddedAt).HasColumnType("datetime2");
        builder.HasIndex(profile => profile.NoticeFetchedAt);
    }
}
