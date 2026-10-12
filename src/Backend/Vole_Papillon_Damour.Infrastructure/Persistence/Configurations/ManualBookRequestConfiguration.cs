using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class ManualBookRequestConfiguration : IEntityTypeConfiguration<ManualBookRequest>
{
    public void Configure(EntityTypeBuilder<ManualBookRequest> builder)
    {
        builder.ToTable("ManualBookRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();

        builder.Property(request => request.RequesterId).IsRequired();
        builder.Property(request => request.Scope)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();
        builder.Property(request => request.WorkId)
            .HasMaxLength(ManualBookRequest.WorkIdMaxLength)
            .HasColumnType("nvarchar(64)");
        builder.Property(request => request.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));
        builder.Property(request => request.Title)
            .HasMaxLength(ManualBookRequest.TextMaxLength)
            .HasColumnType("nvarchar(500)");
        builder.Property(request => request.Authors)
            .HasMaxLength(ManualBookRequest.TextMaxLength)
            .HasColumnType("nvarchar(500)");
        builder.Property(request => request.Publisher)
            .HasMaxLength(ManualBookRequest.TextMaxLength)
            .HasColumnType("nvarchar(500)");
        builder.Property(request => request.Status)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();
        builder.Property(request => request.RequestedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(request => request.FoundOrigin)
            .HasConversion<byte>()
            .HasColumnType("tinyint");
        builder.Property(request => request.FoundIsbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));
        builder.Property(request => request.FoundAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(request => request.FoundBy)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(request => request.FoundScanSessionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? ScanSessionId.Create(value.Value) : null);
        builder.Property(request => request.PickedFromFairId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? AssoEventsId.Create(value.Value) : null);
        builder.Property(request => request.HoldMovementId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? BookMovementId.Create(value.Value) : null);
        builder.Property(request => request.ClosedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(request => request.ClosedBy)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(request => request.ReleaseMovementId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? BookMovementId.Create(value.Value) : null);
        builder.Property(request => request.Version)
            .IsConcurrencyToken();
        builder.Property(request => request.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasOne<ManualRequester>()
            .WithMany()
            .HasForeignKey(request => request.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => new { request.Status, request.Isbn13 })
            .HasDatabaseName("IX_ManualBookRequests_Status_Isbn13");
        builder.HasIndex(request => new { request.Status, request.WorkId })
            .HasDatabaseName("IX_ManualBookRequests_Status_WorkId");
        builder.HasIndex(request => request.RequesterId)
            .HasDatabaseName("IX_ManualBookRequests_RequesterId");
        builder.HasIndex(request => request.HoldMovementId)
            .HasDatabaseName("IX_ManualBookRequests_HoldMovementId");
    }
}
