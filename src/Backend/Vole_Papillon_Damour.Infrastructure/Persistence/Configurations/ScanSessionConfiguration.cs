using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class ScanSessionConfiguration : IEntityTypeConfiguration<ScanSession>
{
    public void Configure(EntityTypeBuilder<ScanSession> builder)
    {
        builder.ToTable("ScanSessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => ScanSessionId.Create(value));
        builder.Property(session => session.VolunteerId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value));
        builder.Property(session => session.Mode)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(session => session.TargetAssoEventsId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? AssoEventsId.Create(value.Value) : null);
        builder.Property(session => session.StartedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(session => session.LastScanAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(session => session.LastSyncAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(session => session.LateArrivals)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(session => session.EndedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(session => session.CloseReason).HasConversion<byte>();
        builder.Property(session => session.Status)
            .HasConversion<byte>()
            .HasDefaultValue(ScanSessionStatus.InProgress)
            .IsRequired();
        builder.Property(session => session.ScannedCount)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(session => session.KeptCount)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(session => session.RejectedCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.VolunteerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssoEvents>()
            .WithMany()
            .HasForeignKey(session => session.TargetAssoEventsId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(session => session.VolunteerId)
            .IsUnique()
            .HasFilter("[Status] = 0");
    }
}
