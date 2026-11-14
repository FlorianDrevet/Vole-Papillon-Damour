using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class AnonymousFavoriteCountConfiguration : IEntityTypeConfiguration<AnonymousFavoriteCount>
{
    public void Configure(EntityTypeBuilder<AnonymousFavoriteCount> builder)
    {
        builder.ToTable("AnonymousFavoriteCounts");
        builder.Ignore(count => count.Isbn13);
        builder.HasKey(count => count.Id);
        builder.Property(count => count.Id)
            .HasColumnName("Isbn13")
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .ValueGeneratedNever()
            .HasConversion(isbn13 => isbn13.Value, value => BookPersistenceConversions.ParseIsbn13(value));
        builder.Property(count => count.Count)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(count => count.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
    }
}
