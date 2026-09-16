using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class RareBookPhotoConfiguration : IEntityTypeConfiguration<RareBookPhoto>
{
    public void Configure(EntityTypeBuilder<RareBookPhoto> builder)
    {
        builder.ToTable("RareBookPhotos");
        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => RareBookPhotoId.Create(value));
        builder.Property(photo => photo.RareBookId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => RareBookId.Create(value));
        builder.Property(photo => photo.BlobUri)
            .HasMaxLength(2048)
            .IsRequired()
            .HasConversion(
                uri => uri.ToString(),
                value => new Uri(value, UriKind.Absolute));
        builder.Property(photo => photo.BlobName)
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(photo => photo.Caption).HasMaxLength(80);
        builder.Property(photo => photo.Position).IsRequired();
        builder.Property(photo => photo.ContentType)
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(photo => photo.SizeBytes).IsRequired();
        builder.Property(photo => photo.UploadedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(photo => photo.UploadedBy)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(photo => photo.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(photo => new { photo.RareBookId, photo.Position })
            .IsUnique();
    }
}
