using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class RareBookTombstoneConfiguration : IEntityTypeConfiguration<RareBookTombstone>
{
    public void Configure(EntityTypeBuilder<RareBookTombstone> builder)
    {
        builder.ToTable("RareBookTombstones");
        builder.HasKey(tombstone => tombstone.RareBookId);
        builder.Property(tombstone => tombstone.RareBookId)
            .ValueGeneratedNever();
        builder.Property(tombstone => tombstone.DeletedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.HasIndex(tombstone => tombstone.DeletedAt);
    }
}
