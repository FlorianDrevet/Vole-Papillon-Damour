using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookIsbndbRecordConfiguration : IEntityTypeConfiguration<BookIsbndbRecord>
{
    public void Configure(EntityTypeBuilder<BookIsbndbRecord> builder)
    {
        builder.ToTable("BookIsbndbRecords");
        builder.HasKey(record => record.Isbn13);
        builder.Property(record => record.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .ValueGeneratedNever()
            .HasConversion(
                isbn13 => isbn13.Value,
                value => BookPersistenceConversions.ParseIsbn13(value));

        builder.Property(record => record.TitleLong).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Isbn10)
            .HasColumnType("char(10)")
            .IsUnicode(false)
            .HasMaxLength(10);
        builder.Property(record => record.Publisher).HasMaxLength(200);
        builder.Property(record => record.Binding).HasMaxLength(100);
        builder.Property(record => record.DatePublished).HasMaxLength(50);
        builder.Property(record => record.Edition).HasMaxLength(100);
        builder.Property(record => record.EditionNumber);
        builder.Property(record => record.Dimensions).HasColumnType("nvarchar(max)");

        ConfigureMeasurement(builder.Property(record => record.HeightValue));
        ConfigureUnit(builder.Property(record => record.HeightUnit));
        ConfigureMeasurement(builder.Property(record => record.WidthValue));
        ConfigureUnit(builder.Property(record => record.WidthUnit));
        ConfigureMeasurement(builder.Property(record => record.LengthValue));
        ConfigureUnit(builder.Property(record => record.LengthUnit));
        ConfigureMeasurement(builder.Property(record => record.WeightValue));
        ConfigureUnit(builder.Property(record => record.WeightUnit));

        builder.Property(record => record.DeweyDecimal).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Synopsis).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Overview).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Excerpt).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Subjects).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Authors).HasColumnType("nvarchar(max)");
        builder.Property(record => record.OtherIsbns).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Related).HasColumnType("nvarchar(max)");
        builder.Property(record => record.Msrp).HasPrecision(18, 2);
        builder.Property(record => record.ListPriceAmount).HasPrecision(18, 2);
        builder.Property(record => record.ListPriceCurrency).HasMaxLength(3);
        builder.Property(record => record.LanguageCode).HasMaxLength(10);
        builder.Property(record => record.ImageUrl).HasMaxLength(2048);
        builder.Property(record => record.RawJson).HasColumnType("nvarchar(max)");
        builder.Property(record => record.FetchedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasOne<Book>()
            .WithOne()
            .HasForeignKey<BookIsbndbRecord>(record => record.Isbn13)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureMeasurement(PropertyBuilder<decimal?> property)
    {
        property.HasPrecision(18, 4);
    }

    private static void ConfigureUnit(PropertyBuilder<string?> property)
    {
        property.HasMaxLength(20);
    }
}
