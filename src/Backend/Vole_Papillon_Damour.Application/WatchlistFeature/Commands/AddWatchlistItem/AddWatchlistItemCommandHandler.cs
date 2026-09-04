using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;

public sealed class AddWatchlistItemCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddWatchlistItemCommand, ErrorOr<AddedWatchlistItemResult>>
{
    public async Task<ErrorOr<AddedWatchlistItemResult>> Handle(
        AddWatchlistItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryBuildTarget(command, out var workId, out var isbn13, out var targetError))
        {
            return targetError;
        }

        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            cancellationToken);
        var addedAt = dateTimeProvider.UtcNow;
        if (addedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Watchlist.InvalidClock",
                "The watchlist clock must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        var watchlist = await dbContext.Watchlists.SingleOrDefaultAsync(
            candidate => candidate.Id == user.Id,
            cancellationToken);
        if (watchlist is null)
        {
            watchlist = Watchlist.Create(user.Id, addedAt);
            dbContext.Watchlists.Add(watchlist);
        }

        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettings.SingletonId,
                cancellationToken);
        var maximumItems = settings?.WatchlistMaxItems ?? 100;
        var existingItems = await dbContext.WatchlistItems
            .Where(item => item.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var duplicate = existingItems.Any(item =>
            item.Scope == command.Scope &&
            (command.Scope == WatchlistItemScope.Work
                ? item.WorkId == workId
                : item.Isbn13 == isbn13));
        if (duplicate)
        {
            return Errors.Watchlist.DuplicateItem();
        }

        if (existingItems.Count >= maximumItems)
        {
            return Errors.Watchlist.LimitReached(maximumItems);
        }

        var item = command.Scope == WatchlistItemScope.Work
            ? WatchlistItem.CreateWork(Guid.NewGuid(), user.Id, workId!, addedAt)
            : WatchlistItem.CreateEdition(Guid.NewGuid(), user.Id, isbn13!.Value, addedAt);
        dbContext.WatchlistItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AddedWatchlistItemResult(
            item.Id,
            item.Scope,
            item.WorkId,
            item.Isbn13?.Value,
            new DateTimeOffset(item.AddedAt, TimeSpan.Zero));
    }

    private static bool TryBuildTarget(
        AddWatchlistItemCommand command,
        out string? workId,
        out Isbn13? isbn13,
        out Error error)
    {
        workId = null;
        isbn13 = null;
        error = Errors.Watchlist.InvalidScope();

        switch (command.Scope)
        {
            case WatchlistItemScope.Work:
                workId = command.WorkId?.Trim();
                if (string.IsNullOrWhiteSpace(workId) || workId.Length > 64 ||
                    !string.IsNullOrWhiteSpace(command.Isbn13))
                {
                    error = Errors.Watchlist.InvalidWorkTarget();
                    return false;
                }

                return true;

            case WatchlistItemScope.Edition:
                if (!string.IsNullOrWhiteSpace(command.WorkId) ||
                    !Isbn13.TryCreate(command.Isbn13, out var parsedIsbn13))
                {
                    error = Errors.Watchlist.InvalidEditionTarget();
                    return false;
                }

                isbn13 = parsedIsbn13;
                return true;

            default:
                error = Errors.Watchlist.InvalidScope();
                return false;
        }
    }
}
