using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminAlertsQueryHandler(
    IBookAlertOutbox bookAlertOutbox,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminAlertsQuery, ErrorOr<AdminAlertPageResult>>
{
    public async Task<ErrorOr<AdminAlertPageResult>> Handle(
        GetAdminAlertsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > 200)
        {
            return Errors.Book.InvalidAdminPage();
        }

        BookAlertQueueStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<BookAlertQueueStatus>(query.Status, true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Error.Validation("Book.InvalidAlertStatus", "The alert status is not supported.");
            }

            status = parsed;
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var page = await bookAlertOutbox.GetAdminPageAsync(
            status,
            query.ScanSessionId,
            query.MemberId,
            query.Page,
            query.PageSize,
            cancellationToken);
        return new AdminAlertPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            page.Items.Select(item => new AdminAlertResult(
                    item.Id,
                    item.ScanSessionId,
                    item.MemberId,
                    item.Status.ToString(),
                    item.ItemCount,
                    item.Attempts,
                    new DateTimeOffset(item.CreatedAt, TimeSpan.Zero),
                    new DateTimeOffset(item.DueAt, TimeSpan.Zero),
                    item.SentAt is { } sentAt ? new DateTimeOffset(sentAt, TimeSpan.Zero) : null,
                    item.LastError))
                .ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize);
    }
}
