using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.Kind)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(message => message.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(message => message.DueAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(message => message.Status)
            .HasConversion<byte>()
            .IsRequired()
            .HasDefaultValue(OutboxMessageStatus.Pending);

        builder.Property(message => message.Attempts)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(message => message.ClaimedUntil)
            .HasColumnType("datetime2");

        builder.Property(message => message.CreatedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(message => message.SentAt)
            .HasColumnType("datetime2");

        builder.Property(message => message.LastError)
            .HasMaxLength(128);

        builder.HasIndex(message => new { message.Status, message.DueAt });
        builder.HasIndex(message => message.ScanSessionId);
    }
}
