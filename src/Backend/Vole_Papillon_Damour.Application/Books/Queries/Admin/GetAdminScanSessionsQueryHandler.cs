using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using ScanSessionEntity = Vole_Papillon_Damour.Domain.ScanSessionAggregate.ScanSession;
using UserName = Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.Name;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminScanSessionsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<GetAdminScanSessionsQuery, ErrorOr<AdminScanSessionPageResult>>
{
    public async Task<ErrorOr<AdminScanSessionPageResult>> Handle(
        GetAdminScanSessionsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > 200)
        {
            return Errors.Book.InvalidAdminPage();
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var from = query.From?.UtcDateTime;
        var to = query.To?.UtcDateTime;
        if (from is not null && from.Value.Kind != DateTimeKind.Utc ||
            to is not null && to.Value.Kind != DateTimeKind.Utc ||
            from is not null && to is not null && from >= to)
        {
            return Error.Validation("Book.InvalidPeriod", "The session period must be valid UTC instants.");
        }

        ScanSessionStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<ScanSessionStatus>(query.Status, true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Error.Validation("Book.InvalidSessionStatus", "The session status is not supported.");
            }

            status = parsed;
        }

        var sessions = await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => status == null || session.Status == status.Value)
            .Where(session => from == null || session.StartedAt >= from.Value)
            .Where(session => to == null || session.StartedAt < to.Value)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .ToListAsync(cancellationToken);
        var totalCount = sessions.Count;
        var page = sessions.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();
        return new AdminScanSessionPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            await BuildResultsAsync(page, cancellationToken),
            totalCount,
            query.Page,
            query.PageSize);
    }

    private async Task<IReadOnlyList<AdminScanSessionResult>> BuildResultsAsync(
        IReadOnlyCollection<ScanSessionEntity> sessions,
        CancellationToken cancellationToken)
    {
        var volunteerIds = sessions.Select(session => session.VolunteerId).ToArray();
        var fairIds = sessions
            .Where(session => session.TargetAssoEventsId is not null)
            .Select(session => session.TargetAssoEventsId!)
            .ToArray();
        var volunteers = await dbContext.Users
            .AsNoTracking()
            .Where(user => volunteerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .Where(fair => fairIds.Contains(fair.Id))
            .ToDictionaryAsync(fair => fair.Id, cancellationToken);

        var results = new List<AdminScanSessionResult>(sessions.Count);
        foreach (var session in sessions)
        {
            var alerts = await bookAlertOutbox.GetAdminPageAsync(
                null,
                session.Id.Value,
                null,
                1,
                1,
                cancellationToken);
            var pendingAlerts = await bookAlertOutbox.GetAdminPageAsync(
                BookAlertQueueStatus.Pending,
                session.Id.Value,
                null,
                1,
                1,
                cancellationToken);
            var firstPending = pendingAlerts.Items.FirstOrDefault();
            var volunteerName = volunteers.TryGetValue(session.VolunteerId, out var volunteer)
                ? FormatName(volunteer.Name)
                : null;
            var fairName = session.TargetAssoEventsId is { } fairId && fairs.TryGetValue(fairId, out var fair)
                ? fair.Name
                : null;
            results.Add(new AdminScanSessionResult(
                session.Id.Value,
                session.VolunteerId.Value,
                volunteerName,
                session.Mode.ToString(),
                session.TargetAssoEventsId?.Value,
                fairName,
                new DateTimeOffset(session.StartedAt, TimeSpan.Zero),
                new DateTimeOffset(session.LastScanAt, TimeSpan.Zero),
                new DateTimeOffset(session.LastSyncAt, TimeSpan.Zero),
                session.EndedAt is { } endedAt ? new DateTimeOffset(endedAt, TimeSpan.Zero) : null,
                session.CloseReason?.ToString(),
                session.Status.ToString(),
                session.ScannedCount,
                session.KeptCount,
                session.RejectedCount,
                alerts.TotalCount,
                pendingAlerts.TotalCount,
                firstPending is null ? null : new DateTimeOffset(firstPending.DueAt, TimeSpan.Zero),
                []));
        }

        return results;
    }

    internal static string? FormatName(UserName? name)
    {
        if (name is null)
        {
            return null;
        }

        var value = $"{name.FirstName} {name.LastName}".Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
