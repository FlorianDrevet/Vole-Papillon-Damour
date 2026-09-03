using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.ToTable("WatchlistItems", table => table.HasCheckConstraint(
            "CK_WatchlistItems_ExactlyOneTarget",
            "(([Scope] = 0 AND [WorkId] IS NOT NULL AND [Isbn13] IS NULL) OR ([Scope] = 1 AND [WorkId] IS NULL AND [Isbn13] IS NOT NULL))"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.UserId)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(item => item.Scope)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(item => item.WorkId).HasMaxLength(64);
        builder.Property(item => item.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));
        builder.Property(item => item.AddedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasOne<Watchlist>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.UserId);
        builder.HasIndex(item => item.WorkId);
        builder.HasIndex(item => item.Isbn13);
    }
}
