using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookMovementConfiguration : IEntityTypeConfiguration<BookMovement>
{
    public void Configure(EntityTypeBuilder<BookMovement> builder)
    {
        builder.ToTable("BookMovements", table => table.HasCheckConstraint(
            "CK_BookMovements_Quantity_NonZero",
            "[Quantity] <> 0"));
        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => BookMovementId.Create(value));

        builder.Property(movement => movement.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .IsRequired()
            .HasConversion(
                isbn13 => isbn13.Value,
                value => BookPersistenceConversions.ParseIsbn13(value));

        builder.Property(movement => movement.Type)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(movement => movement.Quantity).IsRequired();
        builder.Property(movement => movement.OccurredAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(movement => movement.ReceivedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(movement => movement.ClockSuspect)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(movement => movement.ScanSessionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? ScanSessionId.Create(value.Value) : null);
        builder.Property(movement => movement.VolunteerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(movement => movement.AssoEventsId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? AssoEventsId.Create(value.Value) : null);
        builder.Property(movement => movement.Note).HasMaxLength(500);

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(movement => movement.Isbn13)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanSession>()
            .WithMany()
            .HasForeignKey(movement => movement.ScanSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(movement => movement.VolunteerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssoEvents>()
            .WithMany()
            .HasForeignKey(movement => movement.AssoEventsId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(movement => new { movement.Isbn13, movement.OccurredAt });
        builder.HasIndex(movement => new { movement.AssoEventsId, movement.Type });
        builder.HasIndex(movement => movement.ScanSessionId);
        builder.HasIndex(movement => movement.ClientGestureId)
            .IsUnique()
            .HasFilter("[ClientGestureId] IS NOT NULL");
    }
}
