using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public static class IsbndbConfigurations
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IsbndbQuotaState>(builder =>
        {
            builder.ToTable("IsbndbQuotaStates");
            builder.HasKey(state => state.Id);
            builder.Property(state => state.Id).ValueGeneratedNever();
            builder.Property(state => state.DailyLimit).IsRequired();
            builder.Property(state => state.DailyRemaining).IsRequired();
            builder.Property(state => state.ResetsAt)
                .HasColumnType("datetime2")
                .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
            builder.Property(state => state.ObservedAt)
                .HasColumnType("datetime2")
                .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
            builder.Property(state => state.ExhaustedUntil)
                .HasColumnType("datetime2")
                .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
            builder.Property(state => state.LastFailureAt)
                .HasColumnType("datetime2")
                .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
            builder.Property(state => state.LastFailureReason).HasMaxLength(500);
            builder.HasData(new
            {
                Id = IsbndbQuotaState.SingletonId,
                DailyLimit = 5_000,
                DailyRemaining = 5_000,
                ResetsAt = (DateTime?)null,
                ObservedAt = (DateTime?)null,
                ExhaustedUntil = (DateTime?)null,
                LastFailureAt = (DateTime?)null,
                LastFailureReason = (string?)null
            });
        });

        modelBuilder.Entity<IsbndbDailyUsage>(builder =>
        {
            builder.ToTable("IsbndbDailyUsages");
            builder.HasKey(usage => new { usage.Day, usage.Usage });
            builder.Property(usage => usage.Day).HasColumnType("date");
            builder.Property(usage => usage.Usage).HasConversion<byte>();
            builder.Property(usage => usage.Credits).HasDefaultValue(0).IsRequired();
        });

        modelBuilder.Entity<IsbndbCatchUpRun>(builder =>
        {
            builder.ToTable("IsbndbCatchUpRuns");
            builder.HasKey(run => run.Id);
            builder.Property(run => run.Id).ValueGeneratedOnAdd();
            builder.Property(run => run.StartedAt)
                .HasColumnType("datetime2")
                .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
                .IsRequired();
            builder.Property(run => run.EndedAt)
                .HasColumnType("datetime2")
                .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
            builder.Property(run => run.StopReason).HasConversion<byte>();
            builder.Property(run => run.Filled).HasDefaultValue(0).IsRequired();
            builder.Property(run => run.Absent).HasDefaultValue(0).IsRequired();
            builder.Property(run => run.Rechecked).HasDefaultValue(0).IsRequired();
            builder.Property(run => run.Remaining).HasDefaultValue(0).IsRequired();
        });
    }
}
