using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class ManualRequesterConfiguration : IEntityTypeConfiguration<ManualRequester>
{
    public void Configure(EntityTypeBuilder<ManualRequester> builder)
    {
        builder.ToTable("ManualRequesters");
        builder.HasKey(requester => requester.Id);
        builder.Property(requester => requester.Id).ValueGeneratedNever();

        builder.Property(requester => requester.LastName)
            .HasMaxLength(ManualRequester.NameMaxLength)
            .HasColumnType("nvarchar(100)");
        builder.Property(requester => requester.FirstName)
            .HasMaxLength(ManualRequester.NameMaxLength)
            .HasColumnType("nvarchar(100)");
        builder.Property(requester => requester.Phone)
            .HasMaxLength(ManualRequester.PhoneMaxLength)
            .HasColumnType("varchar(16)")
            .IsUnicode(false);
        builder.Property(requester => requester.ConsentRecordedBy)
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(requester => requester.ConsentRecordedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(requester => requester.CreatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(requester => requester.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(requester => requester.ErasedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
    }
}
