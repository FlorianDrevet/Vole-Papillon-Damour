using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class AssociationSettingsConfiguration : IEntityTypeConfiguration<AssociationSettings>
{
    public void Configure(EntityTypeBuilder<AssociationSettings> builder)
    {
        builder.ToTable("AssociationSettings", table => table.HasCheckConstraint(
            "CK_AssociationSettings_SingletonId",
            "[Id] = 1"));
        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();
        builder.Property(settings => settings.DuplicateThreshold)
            .HasDefaultValue(5)
            .IsRequired();
        builder.Property(settings => settings.DemandSalesThreshold)
            .HasDefaultValue(1)
            .IsRequired();
        builder.Property(settings => settings.DeadStockMinAgeDays).IsRequired();
        builder.Property(settings => settings.DeadStockMinQuantity).IsRequired();
        builder.Property(settings => settings.WatchlistMaxItems)
            .HasDefaultValue(100)
            .IsRequired();
        builder.Property(settings => settings.AlertCooldownDays)
            .HasDefaultValue(30)
            .IsRequired();
        builder.Property(settings => settings.SessionIdleTimeoutMinutes)
            .HasDefaultValue(120)
            .IsRequired();
        builder.Property(settings => settings.AlertDelayMinutes)
            .HasDefaultValue(120)
            .IsRequired();
        builder.Property(settings => settings.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(settings => settings.UpdatedBy)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value));

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(settings => settings.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
