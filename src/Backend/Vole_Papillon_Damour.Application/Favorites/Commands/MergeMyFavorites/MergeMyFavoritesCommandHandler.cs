using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.MergeMyFavorites;

public sealed class MergeMyFavoritesCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    FavoriteReferenceService favoriteReferenceService,
    IDateTimeProvider dateTimeProvider,
    IOptions<FavoritesOptions> options)
    : IRequestHandler<MergeMyFavoritesCommand, ErrorOr<MergeMyFavoritesResult>>
{
    public async Task<ErrorOr<MergeMyFavoritesResult>> Handle(
        MergeMyFavoritesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Entries.Count > 200)
        {
            return Errors.Favorites.LimitReached(200);
        }

        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Favorites.InvalidClock", "The favorites clock must be expressed in UTC.");
        }

        var existing = await dbContext.MemberFavoriteBooks
            .Where(favorite => favorite.UserId == user.Id)
            .Select(favorite => favorite.Isbn13)
            .ToListAsync(cancellationToken);
        var remoteIsbns = existing.ToHashSet();
        var localEntries = command.Entries
            .Select((entry, index) => (entry, index, parsed: Isbn13.TryCreate(entry.Isbn13, out var isbn) ? isbn : (Isbn13?)null))
            .Where(item => item.parsed is not null)
            .GroupBy(item => item.parsed!.Value)
            .Select(group => group.OrderByDescending(item => item.entry.AddedAt).ThenBy(item => item.index).First())
            .OrderByDescending(item => item.entry.AddedAt)
            .ThenBy(item => item.parsed!.Value.Value, StringComparer.Ordinal)
            .ToArray();
        var candidates = localEntries.Where(item => !remoteIsbns.Contains(item.parsed!.Value)).ToArray();
        var availableSlots = Math.Max(0, options.Value.AccountMax - existing.Count);
        var selected = candidates.Take(availableSlots).ToArray();
        var skipped = candidates.Length - selected.Length;
        var favoritesToAdd = new List<MemberFavoriteBook>(selected.Length);
        var canonicalIsbns = new HashSet<Isbn13>();

        foreach (var item in selected)
        {
            var isbn13 = item.parsed!.Value;
            var reference = await favoriteReferenceService.EnsureAsync(
                isbn13,
                FavoriteOrigin.Member,
                cancellationToken);
            if (reference.IsError)
            {
                return reference.Errors;
            }

            var canonicalIsbn13 = reference.Value.CanonicalIsbn13;
            if (remoteIsbns.Contains(canonicalIsbn13) || !canonicalIsbns.Add(canonicalIsbn13))
            {
                continue;
            }

            var addedAt = item.entry.AddedAt.Kind == DateTimeKind.Utc && item.entry.AddedAt <= now
                ? item.entry.AddedAt
                : now;
            favoritesToAdd.Add(MemberFavoriteBook.Create(user.Id, canonicalIsbn13, addedAt));
        }

        var favoritesToDecrement = await dbContext.AnonymousFavoriteCounts
            .Where(count => canonicalIsbns.Contains(count.Id))
            .ToListAsync(cancellationToken);
        foreach (var count in favoritesToDecrement)
        {
            count.Decrement(now);
        }

        dbContext.MemberFavoriteBooks.AddRange(favoritesToAdd);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MergeMyFavoritesResult(
            favoritesToAdd.Count,
            skipped,
            existing.Count + favoritesToAdd.Count);
    }
}
