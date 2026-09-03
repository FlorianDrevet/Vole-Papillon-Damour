using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class UserAlertHistoryConfiguration : IEntityTypeConfiguration<UserAlertHistory>
{
    public void Configure(EntityTypeBuilder<UserAlertHistory> builder)
    {
        builder.ToTable("UserAlertHistory");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).ValueGeneratedNever();
        builder.Property(history => history.UserId)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(history => history.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .IsRequired()
            .HasConversion(
                isbn13 => isbn13.Value,
                value => BookPersistenceConversions.ParseIsbn13(value));
        builder.Property(history => history.SentAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(history => history.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(history => history.OutboxMessageId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(history => new { history.UserId, history.Isbn13, history.SentAt });
    }
}
