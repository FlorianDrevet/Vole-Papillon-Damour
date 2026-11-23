using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Domain.AdminDashboardAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record RefreshAdminDashboardSnapshotCommand
    : IRequest<ErrorOr<AdminDashboardSnapshotRefreshResult>>;

public sealed class RefreshAdminDashboardSnapshotCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RefreshAdminDashboardSnapshotCommand, ErrorOr<AdminDashboardSnapshotRefreshResult>>
{
    private static readonly TimeSpan UnmatchedSubjectsRefreshInterval = TimeSpan.FromHours(24);

    private sealed record UnmatchedSubjectSnapshotItem(string Subject, int Count);

    public async Task<ErrorOr<AdminDashboardSnapshotRefreshResult>> Handle(
        RefreshAdminDashboardSnapshotCommand command,
        CancellationToken cancellationToken)
    {
        var computedAt = dateTimeProvider.UtcNow;
        if (computedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);
        var deadStockMinAgeDays = settings?.DeadStockMinAgeDays ?? 180;
        var deadStockMinQuantity = settings?.DeadStockMinQuantity ?? 1;
        var deadStockCutoff = computedAt.AddDays(-deadStockMinAgeDays);
        var canonicalBooks = dbContext.Books
            .AsNoTracking()
            .Where(book => book.RedirectedToIsbn13 == null);
        var deadStockCount = await canonicalBooks
            .Where(book =>
                book.QuantityAvailable > deadStockMinQuantity &&
                !dbContext.BookMovements.Any(movement =>
                    movement.Isbn13 == book.Id &&
                    movement.Type == BookMovementType.Sale))
            .Select(book => dbContext.BookMovements
                .Where(AdminQueryProjection.AffectsAvailableQuantity)
                .Where(movement => movement.Isbn13 == book.Id && movement.Quantity > 0)
                .Min(movement => (DateTime?)movement.OccurredAt))
            .CountAsync(firstAvailableAt => firstAvailableAt <= deadStockCutoff, cancellationToken);

        var snapshot = await dbContext.AdminDashboardSnapshots
            .SingleOrDefaultAsync(candidate => candidate.Id == AdminDashboardSnapshot.SingletonId, cancellationToken);
        if (snapshot is null)
        {
            snapshot = AdminDashboardSnapshot.Create(
                deadStockCount,
                deadStockMinAgeDays,
                deadStockMinQuantity,
                computedAt);
            dbContext.AdminDashboardSnapshots.Add(snapshot);
        }
        else
        {
            snapshot.Refresh(deadStockCount, deadStockMinAgeDays, deadStockMinQuantity, computedAt);
        }

        if (snapshot.IsbndbUnmatchedSubjectsComputedAt is not { } subjectsComputedAt ||
            subjectsComputedAt < computedAt.Subtract(UnmatchedSubjectsRefreshInterval))
        {
            var unmatchedSubjects = await LoadUnmatchedSubjectsAsync(dbContext, cancellationToken);
            snapshot.RefreshIsbndbUnmatchedSubjects(
                JsonSerializer.Serialize(unmatchedSubjects),
                computedAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdminDashboardSnapshotRefreshResult(
            deadStockCount,
            new DateTimeOffset(computedAt, TimeSpan.Zero));
    }

    private static async Task<IReadOnlyList<UnmatchedSubjectSnapshotItem>> LoadUnmatchedSubjectsAsync(
        IProjectDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var unmatched = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var records = dbContext.BookIsbndbRecords.AsNoTracking()
            .Select(record => new { record.Subjects, record.Publisher, record.Authors })
            .AsAsyncEnumerable();

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            var subjects = DeserializeStrings(record.Subjects);
            var authors = DeserializeStrings(record.Authors);
            foreach (var subject in IsbndbGenreMapper.Map(subjects, record.Publisher, authors).UnmatchedSubjects)
            {
                unmatched[subject] = unmatched.GetValueOrDefault(subject) + 1;
            }
        }

        return unmatched.OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(pair => new UnmatchedSubjectSnapshotItem(pair.Key, pair.Value))
            .ToArray();
    }

    private static IReadOnlyList<string>? DeserializeStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
