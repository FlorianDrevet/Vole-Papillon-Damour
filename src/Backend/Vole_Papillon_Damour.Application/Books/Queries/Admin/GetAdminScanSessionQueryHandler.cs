using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminScanSessionQueryHandler(
    IProjectDbContext dbContext,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<GetAdminScanSessionQuery, ErrorOr<AdminScanSessionResult>>
{
    public async Task<ErrorOr<AdminScanSessionResult>> Handle(
        GetAdminScanSessionQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ScanSessionId == Guid.Empty)
        {
            return Errors.Book.ScanSessionNotFound(query.ScanSessionId);
        }

        var session = await dbContext.ScanSessions.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == ScanSessionId.Create(query.ScanSessionId),
            cancellationToken);
        if (session is null)
        {
            return Errors.Book.ScanSessionNotFound(query.ScanSessionId);
        }

        var volunteer = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == session.VolunteerId,
            cancellationToken);
        var fair = session.TargetAssoEventsId is null
            ? null
            : await dbContext.AssoEvents.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.Id == session.TargetAssoEventsId,
                cancellationToken);
        var movements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement => movement.ScanSessionId == session.Id)
            .OrderBy(movement => movement.OccurredAt)
            .ThenBy(movement => movement.Id)
            .ToListAsync(cancellationToken);
        var alerts = await bookAlertOutbox.GetAdminPageAsync(
            null,
            session.Id.Value,
            null,
            1,
            200,
            cancellationToken);
        var pendingAlerts = await bookAlertOutbox.GetAdminPageAsync(
            BookAlertQueueStatus.Pending,
            session.Id.Value,
            null,
            1,
            1,
            cancellationToken);
        var firstPending = pendingAlerts.Items.FirstOrDefault();
        return new AdminScanSessionResult(
            session.Id.Value,
            session.VolunteerId.Value,
            GetAdminScanSessionsQueryHandler.FormatName(volunteer?.Name),
            session.Mode.ToString(),
            session.TargetAssoEventsId?.Value,
            fair?.Name,
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
            movements.Select(AdminQueryProjection.ToMovementResult).ToArray());
    }
}
