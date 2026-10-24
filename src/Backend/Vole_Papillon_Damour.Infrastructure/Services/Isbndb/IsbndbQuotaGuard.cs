using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

public sealed class IsbndbQuotaGuard(
    IProjectDbContext dbContext,
    IMemoryCache memoryCache,
    TimeProvider timeProvider,
    ILogger<IsbndbQuotaGuard> logger) : IIsbndbQuotaGuard
{
    private const string ExhaustionCacheKey = "isbndb:quota:exhausted-until";

    public async Task<bool> IsExhaustedAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(ExhaustionCacheKey, out DateTime? cachedUntil))
        {
            return cachedUntil is { } expiry && expiry > now;
        }

        DateTime? exhaustedUntil;
        try
        {
            exhaustedUntil = await dbContext.IsbndbQuotaStates
                .AsNoTracking()
                .Where(state => state.Id == IsbndbQuotaState.SingletonId)
                .Select(state => state.ExhaustedUntil)
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // If the shared quota state cannot be read, the caller falls back to the
            // existing bibliographic sources instead of spending an untracked credit.
            throw;
        }

        memoryCache.Set(ExhaustionCacheKey, exhaustedUntil, TimeSpan.FromSeconds(30));
        return exhaustedUntil is { } until && until > now;
    }

    public Task ObserveAsync(
        int dailyLimit,
        int dailyRemaining,
        DateTime resetsAt,
        DateTime observedAt,
        CancellationToken cancellationToken) =>
        WriteSafelyAsync(
            "quota observation",
            async token =>
            {
                var state = await GetOrCreateQuotaStateAsync(token);
                state.Observe(dailyLimit, dailyRemaining, resetsAt, observedAt);
                await dbContext.SaveChangesAsync(token);
            },
            () =>
            {
                if (dailyRemaining == 0)
                {
                    memoryCache.Set(ExhaustionCacheKey, (DateTime?)resetsAt, TimeSpan.FromSeconds(30));
                }
                else
                {
                    memoryCache.Remove(ExhaustionCacheKey);
                }
            },
            cancellationToken);

    public Task MarkExhaustedAsync(DateTime resetsAt, DateTime at, CancellationToken cancellationToken) =>
        WriteSafelyAsync(
            "quota exhaustion",
            async token =>
            {
                var state = await GetOrCreateQuotaStateAsync(token);
                state.MarkExhausted(resetsAt, at);
                await dbContext.SaveChangesAsync(token);
            },
            () => memoryCache.Set(ExhaustionCacheKey, (DateTime?)resetsAt, TimeSpan.FromSeconds(30)),
            cancellationToken);

    public Task RecordFailureAsync(string reason, DateTime at, CancellationToken cancellationToken) =>
        WriteSafelyAsync(
            "failure state",
            async token =>
            {
                var state = await GetOrCreateQuotaStateAsync(token);
                state.RecordFailure(reason, at);
                await dbContext.SaveChangesAsync(token);
            },
            null,
            cancellationToken);

    public Task RecordUsageAsync(IsbndbUsage usage, int credits, CancellationToken cancellationToken)
    {
        if (credits <= 0)
        {
            return Task.CompletedTask;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return WriteSafelyAsync(
            "daily usage",
            async token =>
            {
                var dailyUsage = await dbContext.IsbndbDailyUsages
                    .SingleOrDefaultAsync(
                        row => row.Day == today && row.Usage == usage,
                        token);
                if (dailyUsage is null)
                {
                    dailyUsage = IsbndbDailyUsage.Create(today, usage);
                    dbContext.IsbndbDailyUsages.Add(dailyUsage);
                }

                dailyUsage.Increment(credits);
                await dbContext.SaveChangesAsync(token);
            },
            null,
            cancellationToken);
    }

    private async Task<IsbndbQuotaState> GetOrCreateQuotaStateAsync(CancellationToken cancellationToken)
    {
        var state = await dbContext.IsbndbQuotaStates
            .SingleOrDefaultAsync(
                item => item.Id == IsbndbQuotaState.SingletonId,
                cancellationToken);
        if (state is not null)
        {
            return state;
        }

        state = new IsbndbQuotaState();
        dbContext.IsbndbQuotaStates.Add(state);
        return state;
    }

    private async Task WriteSafelyAsync(
        string operationName,
        Func<CancellationToken, Task> write,
        Action? onSuccess,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await write(cancellationToken);
                onSuccess?.Invoke();
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                DetachTrackedQuotaEntities();
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                DetachTrackedQuotaEntities();
            }
            catch (Exception)
            {
                memoryCache.Remove(ExhaustionCacheKey);
                logger.LogWarning(
                    "Could not persist ISBNdb {Operation}; continuing without failing the book request.",
                    operationName);
                return;
            }
        }

        memoryCache.Remove(ExhaustionCacheKey);
        logger.LogWarning(
            "Could not persist ISBNdb {Operation} after one retry; continuing without failing the book request.",
            operationName);
    }

    private void DetachTrackedQuotaEntities()
    {
        if (dbContext is not DbContext efContext)
        {
            return;
        }

        foreach (var entry in efContext.ChangeTracker.Entries<IsbndbQuotaState>().ToArray())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in efContext.ChangeTracker.Entries<IsbndbDailyUsage>().ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }
}
