using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        fixture.Context.BookIsbndbRecords.Add(BookIsbndbRecord.Create(
            absentIsbn,
            Now.AddDays(-2),
            authors: "[\"Hugo, Victor\"]",
            subjects: "[\"Romans\",\"Robinsonades\",\"Self Service\"]"));

        var run = IsbndbCatchUpRun.Create(Now.AddHours(-2));
        run.Finish(Now.AddHours(-1), 1, 2, 0, 7, IsbndbCatchUpStopReason.Completed);
        fixture.Context.IsbndbCatchUpRuns.Add(run);
        await fixture.Context.SaveChangesAsync();

        var handler = new GetIsbndbStatusQueryHandler(fixture.Context, new FixedDateTimeProvider(Now));
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

    private sealed class FixedDateTimeProvider(DateTime now) : Application.Common.Interfaces.Services.IDateTimeProvider
    {
        public DateTime UtcNow => now;
    }
}
