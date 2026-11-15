using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbStatus;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetSummary;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportSummary;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBooks;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminNavigationCountsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 17, 2, 0, DateTimeKind.Utc);
    private static readonly UserId VolunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Counts_match_the_existing_administration_query_sources()
    {
        await using var fixture = await CreatePopulatedFixtureAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new GetAdminNavigationCountsQueryHandler(fixture.Context, clock);

        var counts = await handler.Handle(new GetAdminNavigationCountsQuery(true, false), default);
        var notFound = await new GetNotFoundReportSummaryQueryHandler(fixture.Context, clock)
            .Handle(new GetNotFoundReportSummaryQuery(null, null, false), default);
        var manualRequests = await new GetManualRequestSummaryQueryHandler(fixture.Context, clock)
            .Handle(new GetManualRequestSummaryQuery(), default);
        var catalogue = await new GetAdminBooksQueryHandler(fixture.Context, clock)
            .Handle(new GetAdminBooksQuery(null, null, null, null), default);
        var queue = await new GetBookMetadataQueueQueryHandler(fixture.Context, clock)
            .Handle(new GetBookMetadataQueueQuery(), default);
        var isbndb = await new GetIsbndbStatusQueryHandler(fixture.Context, clock)
            .Handle(new GetIsbndbStatusQuery(), default);
        var rareBooks = await new GetAdminRareBooksQueryHandler(fixture.Context, clock)
            .Handle(new GetAdminRareBooksQuery(Status: "Published"), default);
        var openSessions = await fixture.Context.ScanSessions
            .CountAsync(session => session.Status == ScanSessionStatus.InProgress);

        counts.IsError.Should().BeFalse();
        notFound.IsError.Should().BeFalse();
        manualRequests.IsError.Should().BeFalse();
        catalogue.IsError.Should().BeFalse();
        queue.IsError.Should().BeFalse();
        rareBooks.IsError.Should().BeFalse();
        counts.Value.OpenScanSessions.Should().Be(openSessions);
        counts.Value.NotFoundOpenTargets.Should().Be(notFound.Value.OpenTargetCount);
        counts.Value.NotFoundOverdueTargets.Should().Be(notFound.Value.OverdueTargetCount);
        counts.Value.ManualRequestsFound.Should().Be(manualRequests.Value.FoundCount);
        counts.Value.ManualRequestsFoundOverdue.Should().Be(manualRequests.Value.FoundOverdueCount);
        counts.Value.CatalogueBooks.Should().Be(catalogue.Value.TotalCount);
        counts.Value.IsbnQueue.Should().Be(queue.Value.TotalCount);
        counts.Value.IsbndbAbsentToComplete.Should().Be(isbndb.AbsentToComplete);
        counts.Value.PublishedRareBooks.Should().Be(rareBooks.Value.TotalCount);
    }

    [Fact]
    public async Task Rare_books_volunteers_receive_only_rare_book_navigation_counts()
    {
        await using var fixture = await CreatePopulatedFixtureAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var counts = await new GetAdminNavigationCountsQueryHandler(fixture.Context, clock)
            .Handle(new GetAdminNavigationCountsQuery(false, true), default);
        var rareOnlyNotFound = await new GetNotFoundReportSummaryQueryHandler(fixture.Context, clock)
            .Handle(new GetNotFoundReportSummaryQuery(null, null, true), default);
        var publishedRareBooks = await new GetAdminRareBooksQueryHandler(fixture.Context, clock)
            .Handle(new GetAdminRareBooksQuery(Status: "Published"), default);

        counts.IsError.Should().BeFalse();
        rareOnlyNotFound.IsError.Should().BeFalse();
        publishedRareBooks.IsError.Should().BeFalse();
        counts.Value.OpenScanSessions.Should().BeNull();
        counts.Value.NotFoundOpenTargets.Should().Be(rareOnlyNotFound.Value.OpenTargetCount);
        counts.Value.NotFoundOverdueTargets.Should().Be(rareOnlyNotFound.Value.OverdueTargetCount);
        counts.Value.ManualRequestsFound.Should().BeNull();
        counts.Value.ManualRequestsFoundOverdue.Should().BeNull();
        counts.Value.CatalogueBooks.Should().BeNull();
        counts.Value.IsbnQueue.Should().BeNull();
        counts.Value.IsbndbAbsentToComplete.Should().BeNull();
        counts.Value.PublishedRareBooks.Should().Be(publishedRareBooks.Value.TotalCount);
    }

    private static async Task<ScanBookFixture> CreatePopulatedFixtureAsync()
    {
        var fixture = await ScanBookFixture.CreateAsync();
        var catalogueBook = await fixture.AddBookAsync("9782070363735", 0);
        catalogueBook.ApplyManualMetadata(
            new BookMetadataPatch("Catalogue", null, null, null, null, null, null, null,
                [BookMetadataField.Title]),
            Now);
        var publishedRareBook = await fixture.AddRareBookAsync(catalogueBook, VolunteerId);
        await fixture.AddRareBookAsync(
            await fixture.AddBookAsync("9782070612758", 0), VolunteerId, published: false);

        await fixture.AddBookAsync("9782070584628", 0);
        var absentBook = await fixture.AddBookAsync("9782070408504", 0);
        absentBook.MarkAbsentFromIsbndb(Now.AddDays(-2));

        await fixture.AddSessionAsync(ScanMode.AvailableNow);

        var editionReport = BookNotFoundReport.CreateForEdition(
            Guid.NewGuid(), VolunteerId, catalogueBook.Id, "Edition", Now.AddDays(-8));
        var recentEditionReport = BookNotFoundReport.CreateForEdition(
            Guid.NewGuid(), UserId.CreateUnique(), catalogueBook.Id, "Edition récente", Now.AddDays(-1));
        var rareReport = BookNotFoundReport.CreateForRareBook(
            Guid.NewGuid(), VolunteerId, publishedRareBook.Id, "Rare", Now.AddDays(-9));
        fixture.Context.BookNotFoundReports.AddRange(editionReport, recentEditionReport, rareReport);

        var requesterId = Guid.NewGuid();
        fixture.Context.ManualRequesters.Add(ManualRequester.Create(
            requesterId, "Martin", "Jeanne", "0612345678", true, VolunteerId, Now));
        var oldRequest = ManualBookRequest.CreateWork(
            Guid.NewGuid(), requesterId, "OL123W", "Le Petit Prince", null, null, null, Now.AddDays(-20));
        oldRequest.MarkFoundFromStock(catalogueBook.Id, null, null, BookMovementId.CreateUnique(), VolunteerId, Now.AddDays(-15));
        var recentRequest = ManualBookRequest.CreateWork(
            Guid.NewGuid(), requesterId, "OL456W", "Autre livre", null, null, null, Now.AddDays(-2));
        recentRequest.MarkFoundFromStock(catalogueBook.Id, null, null, BookMovementId.CreateUnique(), VolunteerId, Now.AddDays(-1));
        fixture.Context.ManualBookRequests.AddRange(oldRequest, recentRequest);
        await fixture.Context.SaveChangesAsync();

        return fixture;
    }
}
