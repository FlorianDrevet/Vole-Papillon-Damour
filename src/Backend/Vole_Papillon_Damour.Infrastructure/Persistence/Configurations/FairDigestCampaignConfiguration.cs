using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class FairDigestCampaignConfiguration : IEntityTypeConfiguration<FairDigestCampaign>
{
    public void Configure(EntityTypeBuilder<FairDigestCampaign> builder)
    {
        builder.ToTable("FairDigestCampaigns");
        builder.Ignore(campaign => campaign.CampaignId);
        builder.HasKey(campaign => campaign.Id);
        builder.Property(campaign => campaign.Id).ValueGeneratedNever();
        builder.Property(campaign => campaign.AssoEventsId)
            .HasConversion(id => id.Value, value => AssoEventsId.Create(value))
            .IsRequired();
        builder.Property(campaign => campaign.PlannedAtUtc)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(campaign => campaign.SuspendedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(campaign => campaign.SuspendedBy)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(campaign => campaign.StartedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(campaign => campaign.CompletedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(campaign => campaign.SentCount).HasDefaultValue(0).IsRequired();
        builder.Property(campaign => campaign.SkippedCount).HasDefaultValue(0).IsRequired();
        builder.Property(campaign => campaign.FailedCount).HasDefaultValue(0).IsRequired();

        builder.HasIndex(campaign => campaign.AssoEventsId)
            .IsUnique()
            .HasDatabaseName("UX_FairDigestCampaigns_AssoEventsId");
        builder.HasOne<AssoEvents>()
            .WithMany()
            .HasForeignKey(campaign => campaign.AssoEventsId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(campaign => campaign.SuspendedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
