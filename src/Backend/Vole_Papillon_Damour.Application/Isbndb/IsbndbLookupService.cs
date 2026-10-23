using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Isbndb;

public interface IIsbndbLookupService
{
    Task<IsbndbLookup> LookupAsync(
        Isbn13 isbn13,
        IsbndbUsage usage,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Isbn13, IsbndbLookup>> LookupManyAsync(
        IReadOnlyList<Isbn13> isbn13s,
        IsbndbUsage usage,
        CancellationToken cancellationToken);
}

public sealed class IsbndbLookupService : IIsbndbLookupService
{
    private const string CacheKeyPrefix = "isbndb-book:";
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<IsbndbLookup>> InFlight = new(StringComparer.Ordinal);
    private readonly IIsbndbClient _client;
    private readonly HybridCache _cache;
    private readonly HybridCacheEntryOptions _cacheOptions;

    public IsbndbLookupService(IIsbndbClient client, HybridCache cache, TimeSpan cacheDuration)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(cache);
        if (cacheDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheDuration));
        }

        _client = client;
        _cache = cache;
        _cacheOptions = new HybridCacheEntryOptions
        {
            Expiration = cacheDuration,
            LocalCacheExpiration = cacheDuration
        };
    }

    public async Task<IsbndbLookup> LookupAsync(
        Isbn13 isbn13,
        IsbndbUsage usage,
        CancellationToken cancellationToken)
    {
        if (!IsValid(isbn13))
        {
            return new IsbndbLookup.Unavailable("ISBN-13 invalide");
        }

        var key = BuildCacheKey(isbn13);
        if (!TryClaimFlight(key, out var flight, out var sharedTask))
        {
            return await sharedTask!.WaitAsync(cancellationToken);
        }

        try
        {
            var cached = await TryReadCacheAsync(isbn13, cancellationToken);
            if (cached is not null)
            {
                flight.TrySetResult(cached);
                return cached;
            }

            var result = await _client.GetBookAsync(isbn13, usage, cancellationToken);
            if (IsCacheable(result))
            {
                await CacheAsync(isbn13, CacheEntry.FromLookup(result), cancellationToken);
            }

            flight.TrySetResult(result);
            return result;
        }
        catch (Exception exception)
        {
            flight.TrySetException(exception);
            throw;
        }
        finally
        {
            RemoveFlight(key, flight);
        }
    }

    public async Task<IReadOnlyDictionary<Isbn13, IsbndbLookup>> LookupManyAsync(
        IReadOnlyList<Isbn13> isbn13s,
        IsbndbUsage usage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isbn13s);

        var results = new Dictionary<Isbn13, IsbndbLookup>();
        var ownedFlights = new Dictionary<Isbn13, TaskCompletionSource<IsbndbLookup>>();
        var waitingFlights = new Dictionary<Isbn13, Task<IsbndbLookup>>();
        foreach (var isbn13 in isbn13s.Where(IsValid).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = BuildCacheKey(isbn13);
            if (TryClaimFlight(key, out var flight, out var sharedTask))
            {
                ownedFlights.Add(isbn13, flight);
            }
            else
            {
                waitingFlights.Add(isbn13, sharedTask!);
            }
        }

        if (ownedFlights.Count == 0)
        {
            foreach (var (isbn13, task) in waitingFlights)
            {
                results[isbn13] = await task.WaitAsync(cancellationToken);
            }

            return results;
        }

        try
        {
            var uncached = new List<Isbn13>();
            foreach (var (isbn13, flight) in ownedFlights)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cached = await TryReadCacheAsync(isbn13, cancellationToken);
                if (cached is not null)
                {
                    results[isbn13] = cached;
                    flight.TrySetResult(cached);
                }
                else
                {
                    uncached.Add(isbn13);
                }
            }

            if (uncached.Count > 0)
            {
                var bulk = await _client.GetBooksAsync(uncached, usage, cancellationToken);
                if (bulk.Status != IsbndbBulkLookupStatus.Success)
                {
                    foreach (var isbn13 in uncached)
                    {
                        IsbndbLookup result = bulk.Status switch
                        {
                            IsbndbBulkLookupStatus.QuotaExhausted => new IsbndbLookup.QuotaExhausted(bulk.ResetsAt),
                            _ => new IsbndbLookup.Unavailable(bulk.Reason ?? "ISBNdb est indisponible")
                        };
                        results[isbn13] = result;
                        ownedFlights[isbn13].TrySetResult(result);
                    }
                }
                else
                {
                    var missing = bulk.Missing.ToHashSet();
                    foreach (var isbn13 in uncached)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        IsbndbLookup result;
                        if (bulk.Found.TryGetValue(isbn13, out var book))
                        {
                            result = new IsbndbLookup.Found(book);
                        }
                        else if (missing.Contains(isbn13))
                        {
                            result = await _client.GetBookAsync(isbn13, usage, cancellationToken);
                        }
                        else
                        {
                            result = new IsbndbLookup.Unavailable("ISBN absent de la réponse groupée ISBNdb");
                        }

                        if (IsCacheable(result))
                        {
                            await CacheAsync(isbn13, CacheEntry.FromLookup(result), cancellationToken);
                        }

                        results[isbn13] = result;
                        ownedFlights[isbn13].TrySetResult(result);
                    }
                }
            }

            foreach (var (isbn13, task) in waitingFlights)
            {
                results[isbn13] = await task.WaitAsync(cancellationToken);
            }

            return results;
        }
        catch (Exception exception)
        {
            foreach (var flight in ownedFlights.Values)
            {
                flight.TrySetException(exception);
            }

            throw;
        }
        finally
        {
            foreach (var (isbn13, flight) in ownedFlights)
            {
                RemoveFlight(BuildCacheKey(isbn13), flight);
            }
        }
    }

    private ValueTask CacheAsync(Isbn13 isbn13, CacheEntry entry, CancellationToken cancellationToken) =>
        _cache.SetAsync(BuildCacheKey(isbn13), entry, _cacheOptions, cancellationToken: cancellationToken);

    private async ValueTask<IsbndbLookup?> TryReadCacheAsync(Isbn13 isbn13, CancellationToken cancellationToken)
    {
        var key = BuildCacheKey(isbn13);
        var entry = await _cache.GetOrCreateAsync(
            key,
            state: isbn13,
            static (_, _) => ValueTask.FromResult(CacheEntry.Probe),
            _cacheOptions,
            cancellationToken: cancellationToken);
        if (entry.IsProbe || !entry.IsCacheable)
        {
            await _cache.RemoveAsync(key, cancellationToken);
            return null;
        }

        return entry.ToLookup();
    }

    private static bool TryClaimFlight(
        string key,
        out TaskCompletionSource<IsbndbLookup> flight,
        out Task<IsbndbLookup>? sharedTask)
    {
        while (true)
        {
            if (InFlight.TryGetValue(key, out var current))
            {
                flight = current;
                sharedTask = current.Task;
                return false;
            }

            var candidate = new TaskCompletionSource<IsbndbLookup>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (InFlight.TryAdd(key, candidate))
            {
                flight = candidate;
                sharedTask = null;
                return true;
            }
        }
    }

    private static void RemoveFlight(string key, TaskCompletionSource<IsbndbLookup> flight)
    {
        if (InFlight.TryGetValue(key, out var current) && ReferenceEquals(current, flight))
        {
            InFlight.TryRemove(key, out _);
        }
    }

    private static bool IsCacheable(IsbndbLookup lookup) =>
        lookup is IsbndbLookup.Found or IsbndbLookup.NotFound;

    private static bool IsValid(Isbn13 isbn13) =>
        !string.IsNullOrWhiteSpace(isbn13.Value) && Isbn13.TryCreate(isbn13.Value, out var normalized) && normalized == isbn13;

    private static string BuildCacheKey(Isbn13 isbn13) => $"{CacheKeyPrefix}{isbn13.Value}";

    private sealed record CacheEntry(
        CacheEntryKind Kind,
        IsbndbBook? Book = null,
        DateTime? ResetsAt = null,
        string? Reason = null)
    {
        public static CacheEntry Probe { get; } = new(CacheEntryKind.Probe);
        public bool IsProbe => Kind == CacheEntryKind.Probe;
        public bool IsCacheable => Kind is CacheEntryKind.Found or CacheEntryKind.NotFound;

        public static CacheEntry FromLookup(IsbndbLookup lookup) => lookup switch
        {
            IsbndbLookup.Found found => new CacheEntry(CacheEntryKind.Found, found.Book),
            IsbndbLookup.NotFound => new CacheEntry(CacheEntryKind.NotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(lookup))
        };

        public IsbndbLookup ToLookup() => Kind switch
        {
            CacheEntryKind.Found when Book is not null => new IsbndbLookup.Found(Book),
            CacheEntryKind.NotFound => new IsbndbLookup.NotFound(),
            CacheEntryKind.QuotaExhausted => new IsbndbLookup.QuotaExhausted(ResetsAt),
            CacheEntryKind.Unavailable => new IsbndbLookup.Unavailable(Reason ?? "ISBNdb est indisponible"),
            CacheEntryKind.Probe => throw new InvalidOperationException("A cache probe cannot be returned as an ISBNdb lookup."),
            _ => new IsbndbLookup.Unavailable("La notice ISBNdb en cache est invalide")
        };
    }

    private enum CacheEntryKind : byte
    {
        Probe,
        Found,
        NotFound,
        QuotaExhausted,
        Unavailable
    }
}
