using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class MemberSelectionItemConfiguration : IEntityTypeConfiguration<MemberSelectionItem>
{
    public void Configure(EntityTypeBuilder<MemberSelectionItem> builder)
    {
        builder.ToTable("MemberSelectionItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.UserId)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(item => item.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));
        builder.Property(item => item.RareBookId)
            .HasConversion(new ValueConverter<RareBookId?, Guid?>(
                rareBookId => rareBookId == null ? null : rareBookId.Value,
                value => value == null ? null : RareBookId.Create(value.Value)));
        builder.Property(item => item.Status)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(item => item.AddedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(item => item.StatusChangedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(item => item.PurchasedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);

        builder.HasIndex(item => new { item.UserId, item.Isbn13 })
            .IsUnique()
            .HasFilter("[Isbn13] IS NOT NULL");
        builder.HasIndex(item => new { item.UserId, item.RareBookId })
            .IsUnique()
            .HasFilter("[RareBookId] IS NOT NULL");
        builder.HasIndex(item => item.Isbn13);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
