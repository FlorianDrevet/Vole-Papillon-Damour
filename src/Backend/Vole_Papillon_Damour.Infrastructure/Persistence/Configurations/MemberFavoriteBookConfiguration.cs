using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class MemberFavoriteBookConfiguration : IEntityTypeConfiguration<MemberFavoriteBook>
{
    public void Configure(EntityTypeBuilder<MemberFavoriteBook> builder)
    {
        builder.ToTable("MemberFavoriteBooks");
        builder.HasKey(favorite => new { favorite.UserId, favorite.Isbn13 });
        builder.Property(favorite => favorite.UserId)
            .HasConversion(userId => userId.Value, value => UserId.Create(value))
            .IsRequired();
        builder.Property(favorite => favorite.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => isbn13.Value,
                value => BookPersistenceConversions.ParseIsbn13(value))
            .IsRequired();
        builder.Property(favorite => favorite.AddedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasIndex(favorite => favorite.Isbn13)
            .HasDatabaseName("IX_MemberFavoriteBooks_Isbn13");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
