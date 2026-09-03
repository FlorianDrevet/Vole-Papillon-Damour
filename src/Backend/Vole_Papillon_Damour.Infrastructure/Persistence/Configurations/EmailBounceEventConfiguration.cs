using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class EmailBounceEventConfiguration : IEntityTypeConfiguration<EmailBounceEvent>
{
    public void Configure(EntityTypeBuilder<EmailBounceEvent> builder)
    {
        builder.ToTable("EmailBounceEvents");
        builder.HasKey(bounceEvent => bounceEvent.Id);
        builder.Property(bounceEvent => bounceEvent.Id)
            .ValueGeneratedNever();
        builder.Property(bounceEvent => bounceEvent.ProviderEventId)
            .HasMaxLength(EmailBounceEvent.MaxProviderEventIdLength)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(bounceEvent => bounceEvent.UserId)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(bounceEvent => bounceEvent.RecordedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(bounceEvent => bounceEvent.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(bounceEvent => bounceEvent.ProviderEventId)
            .IsUnique();
    }
}
