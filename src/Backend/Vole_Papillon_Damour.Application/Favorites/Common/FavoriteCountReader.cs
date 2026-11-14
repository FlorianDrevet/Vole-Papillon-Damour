using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

namespace Vole_Papillon_Damour.Application.Favorites.Common;

public sealed record FavoriteCountSeed(string Isbn13, string? WorkId);

public static class FavoriteCountReader
{
    public static async Task<IReadOnlyDictionary<string, int>> GetCountsAsync(
        IProjectDbContext dbContext,
        IReadOnlyCollection<FavoriteCountSeed> seeds,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var uniqueSeeds = seeds
            .GroupBy(seed => seed.Isbn13, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var groupKeysByIsbn = uniqueSeeds.ToDictionary(
            seed => seed.Isbn13,
            seed => GetGroupKey(seed),
            StringComparer.Ordinal);
        var groups = uniqueSeeds
            .GroupBy(GetGroupKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(seed => seed.Isbn13).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var groupKeysByWorkId = uniqueSeeds
            .Where(seed => !string.IsNullOrWhiteSpace(seed.WorkId))
            .GroupBy(seed => seed.WorkId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => GetGroupKey(group.First()), StringComparer.Ordinal);
        var seedIsbns = uniqueSeeds
            .Select(seed => ParseIsbn(seed.Isbn13))
            .ToArray();
        var workIds = groupKeysByWorkId.Keys.ToArray();

        var relatedBooks = await dbContext.Books
            .AsNoTracking()
            .Where(book => seedIsbns.Contains(book.Id) ||
                           (book.WorkId != null && workIds.Contains(book.WorkId)))
            .Select(book => new { book.Id, book.WorkId })
            .ToListAsync(cancellationToken);
        foreach (var book in relatedBooks)
        {
            var isbn = book.Id.Value;
            var groupKey = book.WorkId is not null && groupKeysByWorkId.TryGetValue(book.WorkId, out var workGroupKey)
                ? workGroupKey
                : groupKeysByIsbn.GetValueOrDefault(isbn);
            if (groupKey is not null)
            {
                groups[groupKey].Add(isbn);
            }
        }

        var seedRecords = await dbContext.BookIsbndbRecords
            .AsNoTracking()
            .Where(record => seedIsbns.Contains(record.Isbn13))
            .ToListAsync(cancellationToken);
        foreach (var record in seedRecords)
        {
            var seedIsbn = record.Isbn13.Value;
            if (!groupKeysByIsbn.TryGetValue(seedIsbn, out var groupKey))
            {
                continue;
            }

            groups[groupKey].UnionWith(ParseOtherIsbns(record.OtherIsbns));
        }

        var groupKeysByCandidateIsbn = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (groupKey, isbns) in groups)
        {
            foreach (var isbn in isbns)
            {
                if (!groupKeysByCandidateIsbn.TryGetValue(isbn, out var keys))
                {
                    keys = [];
                    groupKeysByCandidateIsbn.Add(isbn, keys);
                }

                keys.Add(groupKey);
            }
        }

        var candidateIsbns = groupKeysByCandidateIsbn.Keys
            .Select(ParseIsbn)
            .ToArray();
        var memberFavorites = await dbContext.MemberFavoriteBooks
            .AsNoTracking()
            .Where(favorite => candidateIsbns.Contains(favorite.Isbn13))
            .ToListAsync(cancellationToken);
        var anonymousFavorites = await dbContext.AnonymousFavoriteCounts
            .AsNoTracking()
            .Where(favorite => candidateIsbns.Contains(favorite.Id))
            .ToListAsync(cancellationToken);

        var memberIdsByGroup = groups.Keys.ToDictionary(
            key => key,
            _ => new HashSet<Guid>(),
            StringComparer.Ordinal);
        var anonymousCountsByGroup = groups.Keys.ToDictionary(
            key => key,
            _ => 0L,
            StringComparer.Ordinal);

        foreach (var favorite in memberFavorites)
        {
            if (!groupKeysByCandidateIsbn.TryGetValue(favorite.Isbn13.Value, out var groupKeys))
            {
                continue;
            }

            foreach (var groupKey in groupKeys)
            {
                memberIdsByGroup[groupKey].Add(favorite.UserId.Value);
            }
        }

        foreach (var favorite in anonymousFavorites)
        {
            if (!groupKeysByCandidateIsbn.TryGetValue(favorite.Id.Value, out var groupKeys))
            {
                continue;
            }

            foreach (var groupKey in groupKeys)
            {
                anonymousCountsByGroup[groupKey] += favorite.Count;
            }
        }

        return uniqueSeeds.ToDictionary(
            seed => seed.Isbn13,
            seed => (int)Math.Min(
                int.MaxValue,
                memberIdsByGroup[GetGroupKey(seed)].Count + anonymousCountsByGroup[GetGroupKey(seed)]),
            StringComparer.Ordinal);
    }

    private static string GetGroupKey(FavoriteCountSeed seed) =>
        string.IsNullOrWhiteSpace(seed.WorkId)
            ? $"isbn:{seed.Isbn13}"
            : $"work:{seed.WorkId}";

    private static Isbn13 ParseIsbn(string value)
    {
        if (!Isbn13.TryCreate(value, out var isbn13))
        {
            throw new InvalidOperationException("Favorite count ISBNs must be valid ISBN-13 values.");
        }

        return isbn13;
    }

    private static IEnumerable<string> ParseOtherIsbns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<IsbndbOtherIsbn[]>(json) ?? [])
                .Select(otherIsbn => otherIsbn.Isbn?.Trim())
                .Where(value => Isbn13.TryCreate(value, out _))
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
