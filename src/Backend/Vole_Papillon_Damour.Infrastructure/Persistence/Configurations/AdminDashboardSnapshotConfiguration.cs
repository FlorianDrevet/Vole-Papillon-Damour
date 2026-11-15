using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AdminDashboardAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class AdminDashboardSnapshotConfiguration : IEntityTypeConfiguration<AdminDashboardSnapshot>
{
    public void Configure(EntityTypeBuilder<AdminDashboardSnapshot> builder)
    {
        builder.ToTable("AdminDashboardSnapshots", table => table.HasCheckConstraint(
            "CK_AdminDashboardSnapshots_Singleton",
            "[Id] = 1"));
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id)
            .HasColumnType("tinyint")
            .ValueGeneratedNever();
        builder.Property(snapshot => snapshot.DeadStockCount).IsRequired();
        builder.Property(snapshot => snapshot.DeadStockMinAgeDays).IsRequired();
        builder.Property(snapshot => snapshot.DeadStockMinQuantity).IsRequired();
        builder.Property(snapshot => snapshot.ComputedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
    }
}
