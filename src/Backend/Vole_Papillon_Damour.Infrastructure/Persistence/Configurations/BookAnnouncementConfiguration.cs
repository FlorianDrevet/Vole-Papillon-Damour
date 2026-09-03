using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookAnnouncementConfiguration : IEntityTypeConfiguration<BookAnnouncement>
{
    public void Configure(EntityTypeBuilder<BookAnnouncement> builder)
    {
        builder.ToTable("BookAnnouncements", table => table.HasCheckConstraint(
            "CK_BookAnnouncements_Quantity_Positive",
            "[Quantity] > 0"));
        builder.HasKey(announcement => announcement.Id);

        builder.Property(announcement => announcement.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => BookAnnouncementId.Create(value));

        builder.Property(announcement => announcement.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .IsRequired()
            .HasConversion(
                isbn13 => isbn13.Value,
                value => BookPersistenceConversions.ParseIsbn13(value));

        builder.Property(announcement => announcement.AssoEventsId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? AssoEventsId.Create(value.Value) : null);

        builder.Property(announcement => announcement.Quantity)
            .IsRequired();
        builder.Property(announcement => announcement.Status)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(announcement => announcement.CreatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(announcement => announcement.ReleasedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(announcement => announcement.ScanSessionId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => ScanSessionId.Create(value));

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(announcement => announcement.Isbn13)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssoEvents>()
            .WithMany()
            .HasForeignKey(announcement => announcement.AssoEventsId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanSession>()
            .WithMany()
            .HasForeignKey(announcement => announcement.ScanSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(announcement => new { announcement.AssoEventsId, announcement.Status });
        builder.HasIndex(announcement => announcement.AssoEventsId)
            .HasFilter("[AssoEventsId] IS NULL");
    }
}
