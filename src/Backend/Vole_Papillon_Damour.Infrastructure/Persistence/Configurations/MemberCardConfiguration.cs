using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class MemberCardConfiguration : IEntityTypeConfiguration<MemberCard>
{
    public void Configure(EntityTypeBuilder<MemberCard> builder)
    {
        builder.ToTable("MemberCards");
        builder.HasKey(card => card.Id);
        builder.Property(card => card.Id).ValueGeneratedNever();
        builder.Property(card => card.UserId)
            .IsRequired()
            .HasConversion(userId => userId.Value, value => UserId.Create(value));
        builder.Property(card => card.Version).IsRequired();
        builder.Property(card => card.RecoveryCode)
            .HasMaxLength(11)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(card => card.IssuedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(card => card.RotatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(card => card.RevokedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Ignore(card => card.IsActive);

        builder.HasIndex(card => card.UserId)
            .IsUnique()
            .HasDatabaseName("IX_MemberCards_UserId");
        builder.HasIndex(card => card.RecoveryCode)
            .IsUnique()
            .HasFilter("[RevokedAt] IS NULL")
            .HasDatabaseName("IX_MemberCards_RecoveryCode_Active");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(card => card.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
