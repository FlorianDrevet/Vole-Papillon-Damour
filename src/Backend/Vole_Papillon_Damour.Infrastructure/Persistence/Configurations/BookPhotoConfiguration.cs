using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookPhotoConfiguration : IEntityTypeConfiguration<BookPhoto>
{
    public void Configure(EntityTypeBuilder<BookPhoto> builder)
    {
        builder.ToTable("BookPhotos");
        builder.HasKey(photo => photo.Id);
        builder.Property(photo => photo.Id).ValueGeneratedNever();

        builder.Property(photo => photo.BookIsbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                code => code.Value,
                value => BookPersistenceConversions.ParseBookCode(value))
            .IsRequired();

        builder.Property(photo => photo.BlobUri)
            .HasConversion(
                uri => uri.AbsoluteUri,
                value => new Uri(value, UriKind.Absolute))
            .HasMaxLength(2048)
            .IsRequired();
        builder.Property(photo => photo.BlobName).HasMaxLength(1024).IsRequired();
        builder.Property(photo => photo.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(photo => photo.UploadedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(photo => photo.UploadedBy)
            .HasColumnType("uniqueidentifier")
            .HasConversion(userId => userId!.Value, value => UserId.Create(value))
            .IsRequired();

        builder.HasIndex(photo => new { photo.BookIsbn13, photo.Position })
            .HasDatabaseName("IX_BookPhotos_BookIsbn13_Position");
    }
}