using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class FairDigestDeliveryConfiguration : IEntityTypeConfiguration<FairDigestDelivery>
{
    public void Configure(EntityTypeBuilder<FairDigestDelivery> builder)
    {
        builder.ToTable("FairDigestDeliveries");
        builder.HasKey(delivery => new { delivery.CampaignId, delivery.UserId });
        builder.Property(delivery => delivery.CampaignId).ValueGeneratedNever();
        builder.Property(delivery => delivery.UserId)
            .HasConversion(userId => userId.Value, value => UserId.Create(value))
            .IsRequired();
        builder.Property(delivery => delivery.Status)
            .HasConversion<byte>()
            .IsRequired();
        builder.Property(delivery => delivery.SentAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(delivery => delivery.Error).HasMaxLength(500);

        builder.HasOne<FairDigestCampaign>()
            .WithMany()
            .HasForeignKey(delivery => delivery.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(delivery => delivery.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
