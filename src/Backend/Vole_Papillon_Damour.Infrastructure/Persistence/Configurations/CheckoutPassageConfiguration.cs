using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class CheckoutPassageConfiguration : IEntityTypeConfiguration<CheckoutPassage>
{
    public void Configure(EntityTypeBuilder<CheckoutPassage> builder)
    {
        builder.ToTable("CheckoutPassages");
        builder.HasKey(passage => passage.Id);
        builder.Property(passage => passage.Id).ValueGeneratedNever();
        builder.Property(passage => passage.UserId)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(passage => passage.Status)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(passage => passage.OccurredAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(passage => passage.CreatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(passage => passage.AssociatedByVolunteerId)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(passage => passage.AssociatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(passage => passage.UnresolvedReason)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.Property(passage => passage.DissociatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(passage => passage.DissociatedByUserId)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);

        builder.HasIndex(passage => new { passage.UserId, passage.OccurredAt })
            .IsDescending(false, true);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(passage => passage.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
