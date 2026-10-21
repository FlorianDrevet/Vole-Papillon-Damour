using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookGenreConfiguration : IEntityTypeConfiguration<BookGenre>
{
    public void Configure(EntityTypeBuilder<BookGenre> builder)
    {
        builder.ToTable("BookGenres");
        builder.HasKey(genre => new {genre.BookIsbn13, genre.Name});

        builder.Property(genre => genre.BookIsbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => isbn13.Value,
                value => BookPersistenceConversions.ParseIsbn13(value));

        builder.Property(genre => genre.Name)
            .HasMaxLength(100)
            .UseCollation("Latin1_General_100_CI_AI")
            .IsRequired();

        builder.HasIndex(genre => new {genre.Name, genre.BookIsbn13})
            .HasDatabaseName("IX_BookGenres_Name_BookIsbn13");
    }
}
