using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookRecommendationJobConfiguration : IEntityTypeConfiguration<BookRecommendationJob>
{
    public void Configure(EntityTypeBuilder<BookRecommendationJob> builder)
    {
        builder.ToTable("BookRecommendationJobs");
        builder.Ignore(job => job.Id);
        builder.HasKey(job => job.Isbn13);
        builder.Property(job => job.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .ValueGeneratedNever();
        builder.Property(job => job.Status).HasConversion<byte>().IsRequired();
        builder.Property(job => job.Stage).HasConversion<byte>().IsRequired();
        builder.Property(job => job.Revision).IsConcurrencyToken();
        builder.Property(job => job.RequestedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(job => job.StartedAt).HasColumnType("datetime2");
        builder.Property(job => job.CompletedAt).HasColumnType("datetime2");
        builder.Property(job => job.LastError).HasMaxLength(2000);
        builder.HasIndex(job => new { job.Status, job.RequestedAt });
    }
}
