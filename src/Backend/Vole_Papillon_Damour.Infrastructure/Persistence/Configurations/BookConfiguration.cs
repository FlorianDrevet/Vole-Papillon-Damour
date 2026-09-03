using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books", table => table.HasCheckConstraint(
            "CK_Books_NoSelfRedirect",
            "[RedirectedToIsbn13] IS NULL OR [RedirectedToIsbn13] <> [Isbn13]"));
        builder.HasKey(book => book.Id);
        builder.Ignore(book => book.Isbn13);

        builder.Property(book => book.Id)
            .HasColumnName("Isbn13")
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .ValueGeneratedNever()
            .HasConversion(
                isbn13 => isbn13.Value,
                value => BookPersistenceConversions.ParseIsbn13(value));

        builder.Property(book => book.RedirectedToIsbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));

        builder.Property(book => book.WorkId).HasMaxLength(64);
        builder.Property(book => book.Title)
            .HasMaxLength(500)
            .UseCollation("Latin1_General_100_CI_AI");
        builder.Property(book => book.Authors)
            .HasMaxLength(500)
            .UseCollation("Latin1_General_100_CI_AI");
        builder.Property(book => book.Publisher).HasMaxLength(200);
        builder.Property(book => book.PhysicalFormat).HasMaxLength(50);
        builder.Property(book => book.Language).HasMaxLength(10);
        builder.Property(book => book.Genre).HasMaxLength(100);
        builder.Property(book => book.CoverBlobRef).HasMaxLength(200);

        builder.Property(book => book.QuantityAvailable)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(book => book.SalesCount)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(book => book.RejectionCount)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(book => book.IsRare)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(book => book.IsHiddenFromCatalog)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(book => book.MetadataStatus)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(book => book.MetadataSource)
            .HasConversion<byte>();
        builder.Property(book => book.MetadataFetchedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(book => book.ResolveAttempts)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(book => book.LastAttemptAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(book => book.RawPayload).HasColumnType("nvarchar(max)");
        builder.Property(book => book.ManuallyEditedFields).HasColumnType("nvarchar(max)");

        builder.Property(book => book.FirstSeenAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(book => book.LastAvailableAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(book => book.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(book => book.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(book => book.WorkId);
        builder.HasIndex(book => new { book.MetadataStatus, book.LastAttemptAt });
        builder.HasIndex(book => book.UpdatedAt);

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(book => book.RedirectedToIsbn13)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
