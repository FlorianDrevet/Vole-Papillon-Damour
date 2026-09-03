using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class WatchlistConfiguration : IEntityTypeConfiguration<Watchlist>
{
    public void Configure(EntityTypeBuilder<Watchlist> builder)
    {
        builder.ToTable("Watchlists");
        builder.HasKey(watchlist => watchlist.Id);
        builder.Property(watchlist => watchlist.Id)
            .HasColumnName("UserId")
            .ValueGeneratedNever()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(watchlist => watchlist.AlertStatus)
            .HasConversion<byte>()
            .HasDefaultValue(WatchlistAlertStatus.Active)
            .IsRequired();
        builder.Property(watchlist => watchlist.BounceCount)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(watchlist => watchlist.CreatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<Watchlist>(watchlist => watchlist.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
