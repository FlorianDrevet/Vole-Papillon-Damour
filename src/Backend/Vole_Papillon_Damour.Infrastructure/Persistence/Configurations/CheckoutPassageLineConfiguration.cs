using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class CheckoutPassageLineConfiguration : IEntityTypeConfiguration<CheckoutPassageLine>
{
    public void Configure(EntityTypeBuilder<CheckoutPassageLine> builder)
    {
        builder.ToTable("CheckoutPassageLines");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.CheckoutPassageId).IsRequired();
        builder.Property(line => line.SaleMovementId)
            .HasConversion(new ValueConverter<BookMovementId?, Guid?>(
                movementId => movementId == null ? null : movementId.Value,
                value => value == null ? null : BookMovementId.Create(value.Value)));
        builder.Property(line => line.RareBookId)
            .HasConversion(new ValueConverter<RareBookId?, Guid?>(
                rareBookId => rareBookId == null ? null : rareBookId.Value,
                value => value == null ? null : RareBookId.Create(value.Value)));
        builder.Property(line => line.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));
        builder.Property(line => line.RequestedIsbn13)
            .HasMaxLength(13)
            .IsUnicode(false);
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.Title)
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(line => line.Authors).HasMaxLength(500);
        builder.Property(line => line.Publisher).HasMaxLength(200);
        builder.Property(line => line.PublicationYear);
        builder.Property(line => line.PhysicalFormat).HasMaxLength(100);
        builder.Property(line => line.AssoEventsId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? AssoEventsId.Create(value.Value) : null);
        builder.Property(line => line.OccurredAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(line => line.VoidedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);

        builder.HasIndex(line => line.SaleMovementId)
            .IsUnique()
            .HasFilter("[SaleMovementId] IS NOT NULL");
        builder.HasIndex(line => new { line.CheckoutPassageId, line.RareBookId })
            .IsUnique()
            .HasFilter("[RareBookId] IS NOT NULL");

        builder.HasOne<CheckoutPassage>()
            .WithMany()
            .HasForeignKey(line => line.CheckoutPassageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
