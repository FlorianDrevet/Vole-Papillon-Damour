using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class RareBookConfiguration : IEntityTypeConfiguration<RareBook>
{
    public void Configure(EntityTypeBuilder<RareBook> builder)
    {
        builder.ToTable("RareBooks");
        builder.HasKey(book => book.Id);

        builder.Property(book => book.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => RareBookId.Create(value));

        builder.Property(book => book.Slug)
            .HasMaxLength(RareBookSlug.MaxLength)
            .IsRequired()
            .HasConversion(
                slug => slug.Value,
                value => RareBookSlug.Create(value));

        builder.Property(book => book.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));

        builder.Property(book => book.Title)
            .HasMaxLength(300)
            .IsRequired();
        builder.Property(book => book.AuthorMention).HasMaxLength(300);
        builder.Property(book => book.Publisher).HasMaxLength(200);
        builder.Property(book => book.PublicationYear);
        builder.Property(book => book.Price)
            .HasPrecision(10, 2)
            .IsRequired();
        builder.Property(book => book.Condition)
            .IsRequired()
            .HasConversion(
                condition => (byte)condition.Value,
                value => new RareBookCondition((RareBookCondition.RareBookConditionEnum)value));
        builder.Property(book => book.PublicDescription).HasMaxLength(1200);
        builder.Property(book => book.Status)
            .HasConversion<byte>()
            .HasDefaultValue(RareBookStatus.Draft)
            .IsRequired();
        builder.Property(book => book.IsSold)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(book => book.SoldAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(book => book.SoldAtFairId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? AssoEventsId.Create(value.Value) : null);
        builder.Property(book => book.SoldInSessionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? ScanSessionId.Create(value.Value) : null);
        builder.Property(book => book.CreatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(book => book.CreatedBy)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(book => book.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(book => book.UpdatedBy)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(book => book.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(book => book.ClientGestureId);

        builder.HasIndex(book => book.Slug).IsUnique();
        builder.HasIndex(book => book.Isbn13)
            .IsUnique()
            .HasFilter("[Isbn13] IS NOT NULL");
        builder.HasIndex(book => new { book.Status, book.IsSold, book.Price });
        builder.HasIndex(book => book.ClientGestureId)
            .IsUnique()
            .HasFilter("[ClientGestureId] IS NOT NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(book => book.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(book => book.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssoEvents>()
            .WithMany()
            .HasForeignKey(book => book.SoldAtFairId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanSession>()
            .WithMany()
            .HasForeignKey(book => book.SoldInSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(book => book.Photos)
            .WithOne()
            .HasForeignKey(photo => photo.RareBookId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(RareBook.Photos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
