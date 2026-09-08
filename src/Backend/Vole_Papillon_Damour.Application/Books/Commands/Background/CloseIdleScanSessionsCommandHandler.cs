using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed class CloseIdleScanSessionsCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    CloseScanSessionCommandHandler closeScanSessionHandler)
    : IRequestHandler<CloseIdleScanSessionsCommand, CloseIdleScanSessionsResult>
{
    public async Task<CloseIdleScanSessionsResult> Handle(
        CloseIdleScanSessionsCommand command,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        EnsureUtc(now, nameof(dateTimeProvider.UtcNow));

        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);
        var timeoutMinutes = settings?.SessionIdleTimeoutMinutes ?? 120;
        if (timeoutMinutes <= 0)
        {
            throw new InvalidOperationException(
                "The session inactivity timeout must be positive.");
        }

        var cutoff = now.AddMinutes(-timeoutMinutes);
        var sessions = await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session =>
                session.Status == ScanSessionStatus.InProgress &&
                session.LastScanAt <= cutoff &&
                session.LastSyncAt <= cutoff)
            .OrderBy(session => session.LastSyncAt)
            .ThenBy(session => session.Id)
            .Select(session => new {session.Id, session.VolunteerId})
            .ToListAsync(cancellationToken);

        var closedCount = 0;
        foreach (var session in sessions)
        {
            var result = await closeScanSessionHandler.Handle(
                new CloseScanSessionCommand(
                    session.Id,
                    ScanCloseReason.Inactivity,
                    session.VolunteerId),
                cancellationToken);

            if (!result.IsError &&
                result.Value.Status == ScanSessionStatus.Completed &&
                result.Value.CloseReason == ScanCloseReason.Inactivity &&
                result.Value.EndedAt == now)
            {
                closedCount++;
            }
        }

        return new CloseIdleScanSessionsResult(sessions.Count, closedCount);
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException(
                $"The {parameterName} value must be expressed in UTC.");
        }
    }
}
