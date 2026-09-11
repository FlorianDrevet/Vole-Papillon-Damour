using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class SocialPostImportConfiguration : IEntityTypeConfiguration<SocialPostImport>
{
    public void Configure(EntityTypeBuilder<SocialPostImport> builder)
    {
        builder.ToTable("SocialPostImports");
        builder.HasKey(import => import.Id);
        builder.Property(import => import.Id)
            .ValueGeneratedNever();

        builder.Property(import => import.Source)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(import => import.ExternalId)
            .HasMaxLength(256)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(import => import.Permalink)
            .HasMaxLength(2048)
            .IsUnicode(false)
            .HasConversion(
                permalink => permalink.ToString(),
                value => new Uri(value))
            .IsRequired();
        builder.Property(import => import.PublishedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.Property(import => import.ImportedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.Property(import => import.ActualityId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? ActualityId.Create(value.Value) : null);

        builder.HasOne<Actuality>()
            .WithMany()
            .HasForeignKey(import => import.ActualityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(import => new { import.Source, import.ExternalId })
            .IsUnique();
    }
}
