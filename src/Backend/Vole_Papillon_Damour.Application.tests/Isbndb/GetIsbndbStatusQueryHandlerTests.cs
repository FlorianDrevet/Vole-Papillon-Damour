using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbStatus;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class GetIsbndbStatusQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ReturnsPersistedQuotaUsageCatchUpAndUnmatchedSubjects()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var quota = new IsbndbQuotaState();
        quota.Observe(5_000, 4_321, Now.AddHours(12), Now.AddMinutes(-1));
        quota.RecordFailure("provider unavailable", Now);
        fixture.Context.IsbndbQuotaStates.Add(quota);

        var day = DateOnly.FromDateTime(Now);
        var scanUsage = IsbndbDailyUsage.Create(day, IsbndbUsage.Scan);
        scanUsage.Increment(5);
        var catchUpUsage = IsbndbDailyUsage.Create(day, IsbndbUsage.CatchUp);
        catchUpUsage.Increment(3);
        fixture.Context.IsbndbDailyUsages.AddRange(scanUsage, catchUpUsage);

        Isbn13.TryCreate("9782070363735", out var pendingIsbn).Should().BeTrue();
        var pending = Book.Create(pendingIsbn, Now.AddDays(-2));
        pending.RecordAvailableEntry(Now.AddDays(-1));
        fixture.Context.Books.Add(pending);

        Isbn13.TryCreate("9782070409228", out var absentIsbn).Should().BeTrue();
        var absent = Book.Create(absentIsbn, Now.AddDays(-10));
        absent.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre absent", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-10),
            null);
        absent.MarkAbsentFromIsbndb(Now.AddDays(-2));
        fixture.Context.Books.Add(absent);
        var absentWithoutTitle = await fixture.AddBookAsync("9782253004226", quantityAvailable: 0);
        absentWithoutTitle.MarkAbsentFromIsbndb(Now.AddDays(-1));
        fixture.Context.BookIsbndbRecords.Add(BookIsbndbRecord.Create(
            absentIsbn,
            Now.AddDays(-2),
            authors: "[\"Hugo, Victor\"]",
            subjects: "[\"Romans\",\"Robinsonades\",\"Self Service\"]"));

        var run = IsbndbCatchUpRun.Create(Now.AddHours(-2));
        run.Finish(Now.AddHours(-1), 1, 2, 0, 7, IsbndbCatchUpStopReason.Completed);
        fixture.Context.IsbndbCatchUpRuns.Add(run);
        await fixture.Context.SaveChangesAsync();

        using var cacheServices = CreateHybridCacheProvider();
        var handler = new GetIsbndbStatusQueryHandler(
            fixture.Context,
            new FixedDateTimeProvider(Now),
            cacheServices.GetRequiredService<HybridCache>());
        var result = await handler.Handle(new GetIsbndbStatusQuery(), CancellationToken.None);

        result.DailyLimit.Should().Be(5_000);
        result.DailyRemaining.Should().Be(4_321);
        result.Status.Should().Be("Unavailable");
        result.UsageToday.Should().ContainSingle(item => item.Usage == "Scan" && item.Credits == 5);
        result.UsageToday.Should().ContainSingle(item => item.Usage == "CatchUp" && item.Credits == 3);
        result.PendingCatchUp.Should().Be(new IsbndbPendingCatchUpResult(1, 1, 1));
        result.AbsentToComplete.Should().Be(1);
        result.LastRun.Should().NotBeNull();
        result.LastRun!.Filled.Should().Be(1);
        result.LastRun.Remaining.Should().Be(7);
        result.UnmatchedSubjects.Should().ContainSingle(item => item.Subject == "Robinsonades" && item.Count == 1);
    }

    [Fact]
    public async Task Handle_aggregates_book_counts_and_caches_unmatched_subjects_for_ten_minutes()
    {
        var sql = new SqlCaptureInterceptor();
        await using var fixture = await ScanBookFixture.CreateAsync(sql);
        Isbn13.TryCreate("9782070363735", out var pendingIsbn).Should().BeTrue();
        var pending = Book.Create(pendingIsbn, Now.AddDays(-2));
        pending.RecordAvailableEntry(Now.AddDays(-1));
        fixture.Context.Books.Add(pending);

        Isbn13.TryCreate("9782070409228", out var absentIsbn).Should().BeTrue();
        var absent = Book.Create(absentIsbn, Now.AddDays(-10));
        absent.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre absent", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-10),
            null);
        absent.MarkAbsentFromIsbndb(Now.AddDays(-2));
        fixture.Context.Books.Add(absent);
        fixture.Context.BookIsbndbRecords.Add(BookIsbndbRecord.Create(
            absentIsbn,
            Now.AddDays(-2),
            authors: "[\"Hugo, Victor\"]",
            subjects: "[\"Romans\",\"Robinsonades\"]"));
        await fixture.Context.SaveChangesAsync();

        using var cacheServices = CreateHybridCacheProvider();
        var handler = new GetIsbndbStatusQueryHandler(
            fixture.Context,
            new FixedDateTimeProvider(Now),
            cacheServices.GetRequiredService<HybridCache>());

        sql.Clear();
        var first = await handler.Handle(new GetIsbndbStatusQuery(), CancellationToken.None);

        first.PendingCatchUp.Should().Be(new IsbndbPendingCatchUpResult(1, 1, 1));
        first.AbsentToComplete.Should().Be(1);
        first.UnmatchedSubjects.Should().ContainSingle(item => item.Subject == "Robinsonades" && item.Count == 1);
        sql.Commands.Count(command => command.Contains("\"Books\"", StringComparison.Ordinal))
            .Should().Be(1);

        sql.Clear();
        var second = await handler.Handle(new GetIsbndbStatusQuery(), CancellationToken.None);

        second.UnmatchedSubjects.Should().BeEquivalentTo(first.UnmatchedSubjects);
        sql.Commands.Should().NotContain(command =>
            command.Contains("BookIsbndbRecords", StringComparison.OrdinalIgnoreCase));
    }

    private static ServiceProvider CreateHybridCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private sealed class FixedDateTimeProvider(DateTime now) : Application.Common.Interfaces.Services.IDateTimeProvider
    {
        public DateTime UtcNow => now;
    }

    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public void Clear() => Commands.Clear();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
