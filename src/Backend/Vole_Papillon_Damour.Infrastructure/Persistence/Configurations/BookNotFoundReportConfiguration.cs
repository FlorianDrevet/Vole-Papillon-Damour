using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class BookNotFoundReportConfiguration : IEntityTypeConfiguration<BookNotFoundReport>
{
    public void Configure(EntityTypeBuilder<BookNotFoundReport> builder)
    {
        builder.ToTable("BookNotFoundReports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).ValueGeneratedNever();

        builder.Property(report => report.UserId)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(report => report.Isbn13)
            .HasColumnType("char(13)")
            .IsUnicode(false)
            .HasConversion(
                isbn13 => BookPersistenceConversions.SerializeNullableIsbn13(isbn13),
                value => BookPersistenceConversions.ParseNullableIsbn13(value));
        builder.Property(report => report.RareBookId)
            .HasConversion(
                rareBookId => rareBookId == null ? (Guid?)null : rareBookId.Value,
                value => value.HasValue ? RareBookId.Create(value.Value) : null);
        builder.Property(report => report.Location)
            .HasConversion<byte>();
        builder.Property(report => report.Comment)
            .HasMaxLength(BookNotFoundReport.CommentMaxLength)
            .HasColumnType("nvarchar(280)");
        builder.Property(report => report.Status)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();
        builder.Property(report => report.ReportedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();
        builder.Property(report => report.ClosedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.NullableUtcDateTimeConverter);
        builder.Property(report => report.ClosedBy)
            .HasConversion(
                userId => userId == null ? (Guid?)null : userId.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null);
        builder.Property(report => report.ClosureNote)
            .HasMaxLength(BookNotFoundReport.ClosureNoteMaxLength)
            .HasColumnType("nvarchar(500)");
        builder.Property(report => report.WithdrawalReason)
            .HasConversion<byte>();
        builder.Property(report => report.WithdrawnQuantity);
        builder.Property(report => report.WithdrawalMovementId)
            .HasConversion(
                movementId => movementId == null ? (Guid?)null : movementId.Value,
                value => value.HasValue ? BookMovementId.Create(value.Value) : null);

        builder.HasIndex(report => new { report.Status, report.Isbn13 })
            .HasDatabaseName("IX_BookNotFoundReports_Status_Isbn13");
        builder.HasIndex(report => new { report.Status, report.RareBookId })
            .HasDatabaseName("IX_BookNotFoundReports_Status_RareBookId");
        builder.HasIndex(report => new { report.UserId, report.ReportedAt })
            .HasDatabaseName("IX_BookNotFoundReports_UserId_ReportedAt");
        builder.HasIndex(report => new { report.UserId, report.Isbn13 })
            .IsUnique()
            .HasDatabaseName("UX_BookNotFoundReports_OpenPerMemberEdition")
            .HasFilter("[Status] = 0 AND [Isbn13] IS NOT NULL AND [UserId] IS NOT NULL");
        builder.HasIndex(report => new { report.UserId, report.RareBookId })
            .IsUnique()
            .HasDatabaseName("UX_BookNotFoundReports_OpenPerMemberRareBook")
            .HasFilter("[Status] = 0 AND [RareBookId] IS NOT NULL AND [UserId] IS NOT NULL");
    }
}
