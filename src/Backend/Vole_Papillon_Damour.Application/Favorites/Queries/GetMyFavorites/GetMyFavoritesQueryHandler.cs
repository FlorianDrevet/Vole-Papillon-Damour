using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

namespace Vole_Papillon_Damour.Application.Favorites.Queries.GetMyFavorites;

public sealed class GetMyFavoritesQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider,
    IOptions<FavoritesOptions> options)
    : IRequestHandler<GetMyFavoritesQuery, ErrorOr<MyFavoritesResult>>
{
    public async Task<ErrorOr<MyFavoritesResult>> Handle(
        GetMyFavoritesQuery query,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            query.ExternalId,
            query.Email,
            query.FirstName,
            query.LastName,
            cancellationToken);
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Favorites.InvalidClock", "The favorites clock must be expressed in UTC.");
        }

        var favorites = await dbContext.MemberFavoriteBooks
            .AsNoTracking()
            .Where(favorite => favorite.UserId == user.Id)
            .OrderByDescending(favorite => favorite.AddedAt)
            .ThenBy(favorite => favorite.Isbn13)
            .ToListAsync(cancellationToken);
        if (favorites.Count == 0)
        {
            return new MyFavoritesResult(new DateTimeOffset(now, TimeSpan.Zero), 0, options.Value.AccountMax, []);
        }

        var isbns = favorites.Select(favorite => favorite.Isbn13).ToArray();
        var isbnValues = isbns.Select(isbn => isbn.Value).ToArray();
        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book => isbns.Contains(book.Id))
            .ToListAsync(cancellationToken);
        var booksByIsbn = books.ToDictionary(book => book.Id.Value, StringComparer.Ordinal);
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => isbns.Contains(announcement.Isbn13))
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToReferencedFairListAsync(announcements, cancellationToken);
        var publishedAvailableRareIsbns = (await dbContext.RareBooks
                .AsNoTracking()
                .PublishedAvailable()
                .Where(rareBook => rareBook.Isbn13 != null && isbns.Contains(rareBook.Isbn13.Value))
                .Select(rareBook => rareBook.Isbn13!.Value.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var publicBooks = PublicCatalogProjector.Project(
                books,
                announcements,
                fairs,
                publishedAvailableRareIsbns,
                now)
            .ToDictionary(book => book.Isbn13, StringComparer.Ordinal);

        var memberFavoriteCounts = await dbContext.MemberFavoriteBooks
            .AsNoTracking()
            .Where(favorite => isbns.Contains(favorite.Isbn13))
            .Select(favorite => favorite.Isbn13)
            .ToListAsync(cancellationToken);
        var memberCountByIsbn = memberFavoriteCounts
            .GroupBy(isbn => isbn.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var anonymousCounts = await dbContext.AnonymousFavoriteCounts
            .AsNoTracking()
            .Where(count => isbns.Contains(count.Id))
            .ToListAsync(cancellationToken);
        var anonymousCountByIsbn = anonymousCounts
            .ToDictionary(count => count.Id.Value, count => count.Count, StringComparer.Ordinal);
        var jobs = await dbContext.BookRecommendationJobs
            .AsNoTracking()
            .Where(job => isbnValues.Contains(job.Isbn13))
            .ToDictionaryAsync(job => job.Isbn13, cancellationToken);
        var isbndbRecords = await dbContext.BookIsbndbRecords
            .AsNoTracking()
            .Where(record => isbns.Contains(record.Isbn13))
            .ToListAsync(cancellationToken);

        var workIds = books
            .Where(book => !string.IsNullOrWhiteSpace(book.WorkId))
            .GroupBy(book => book.WorkId!, StringComparer.Ordinal)
            .Select(group => group.Select(book => book.Id.Value).ToArray())
            .ToArray();
        var parent = isbnValues.ToDictionary(isbn => isbn, isbn => isbn, StringComparer.Ordinal);
        foreach (var work in workIds)
        {
            for (var index = 1; index < work.Length; index++)
            {
                Union(parent, work[0], work[index]);
            }
        }

        var favoriteIsbnSet = isbnValues.ToHashSet(StringComparer.Ordinal);
        foreach (var book in books)
        {
            if (book.RedirectedToIsbn13 is { } canonical && favoriteIsbnSet.Contains(canonical.Value))
            {
                Union(parent, book.Id.Value, canonical.Value);
            }
        }

        foreach (var record in isbndbRecords)
        {
            foreach (var otherIsbn in ParseOtherIsbns(record.OtherIsbns))
            {
                if (favoriteIsbnSet.Contains(otherIsbn))
                {
                    Union(parent, record.Isbn13.Value, otherIsbn);
                }
            }
        }

        var publicBookByIsbn = publicBooks;
        var groups = favorites
            .GroupBy(favorite => Find(parent, favorite.Isbn13.Value), StringComparer.Ordinal)
            .Select(group =>
            {
                var editions = group
                    .OrderByDescending(favorite => favorite.AddedAt)
                    .ThenBy(favorite => favorite.Isbn13.Value, StringComparer.Ordinal)
                    .Select(favorite => ProjectEdition(
                        favorite,
                        booksByIsbn.GetValueOrDefault(favorite.Isbn13.Value),
                        publicBookByIsbn.GetValueOrDefault(favorite.Isbn13.Value),
                        memberCountByIsbn.GetValueOrDefault(favorite.Isbn13.Value) +
                        anonymousCountByIsbn.GetValueOrDefault(favorite.Isbn13.Value),
                        jobs.GetValueOrDefault(favorite.Isbn13.Value)))
                    .ToArray();
                var firstBook = group
                    .Select(favorite => booksByIsbn.GetValueOrDefault(favorite.Isbn13.Value))
                    .FirstOrDefault(book => book is not null);
                var title = editions[0].Title;
                var workId = group
                    .Select(favorite => booksByIsbn.GetValueOrDefault(favorite.Isbn13.Value)?.WorkId)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                var workKey = workId is not null ? $"work:{workId}" : $"related:{group.Key}";
                return new MyFavoriteWorkResult(
                    workKey,
                    workId ?? firstBook?.WorkId,
                    title,
                    editions[0].Authors,
                    editions.Max(edition => edition.AddedAt),
                    editions.Length,
                    editions.Any(edition => edition.RecommendationStatus == "Computing"),
                    editions);
            })
            .OrderByDescending(group => group.AddedAt)
            .ThenBy(group => group.WorkKey, StringComparer.Ordinal)
            .ToArray();

        return new MyFavoritesResult(
            new DateTimeOffset(now, TimeSpan.Zero),
            favorites.Count,
            options.Value.AccountMax,
            groups);
    }

    private static MyFavoriteEditionResult ProjectEdition(
        MemberFavoriteBook favorite,
        Book? book,
        PublicCatalogBookResult? publicBook,
        int favoriteCount,
        BookRecommendationJob? job)
    {
        var status = job?.Status is BookRecommendationJobStatus.Ready or BookRecommendationJobStatus.NoNotice
            ? job.Status.ToString()
            : "Computing";
        return new MyFavoriteEditionResult(
            favorite.Isbn13.Value,
            publicBook?.Title ?? book?.Title ?? $"ISBN {favorite.Isbn13.Value}",
            publicBook?.Authors ?? book?.Authors,
            publicBook?.CoverUrl ?? book?.CoverUrl,
            new DateTimeOffset(favorite.AddedAt, TimeSpan.Zero),
            publicBook?.QuantityAvailable ?? 0,
            publicBook?.QuantityAnnounced ?? 0,
            favoriteCount,
            status);
    }

    private static IReadOnlyList<string> ParseOtherIsbns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<IsbndbOtherIsbn[]>(json) ?? [])
                .Select(other => other.Isbn?.Trim())
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

    private static void Union(IDictionary<string, string> parent, string first, string second)
    {
        var firstRoot = Find(parent, first);
        var secondRoot = Find(parent, second);
        if (!string.Equals(firstRoot, secondRoot, StringComparison.Ordinal))
        {
            parent[secondRoot] = firstRoot;
        }
    }

    private static string Find(IDictionary<string, string> parent, string value)
    {
        var current = value;
        while (!string.Equals(parent[current], current, StringComparison.Ordinal))
        {
            parent[current] = parent[parent[current]];
            current = parent[current];
        }

        return current;
    }
}
