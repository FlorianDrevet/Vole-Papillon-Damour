using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Background;

public sealed class BackgroundBookCommandHandlerTests
{
    private static readonly DateTime WorkerNow =
        new(2026, 9, 3, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CloseIdle_WhenBothSessionTimestampsAreStale_ClosesThroughTheRegularHandler()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new CloseIdleScanSessionsCommandHandler(
            fixture.Context,
            clock,
            fixture.CreateCloseScanSessionHandler(WorkerNow));

        var result = await handler.Handle(
            new CloseIdleScanSessionsCommand(),
            CancellationToken.None);

        result.ClosedCount.Should().Be(1);
        var persisted = await fixture.Context.ScanSessions.SingleAsync();
        persisted.Status.Should().Be(ScanSessionStatus.Completed);
        persisted.CloseReason.Should().Be(ScanCloseReason.Inactivity);
        await fixture.AlertOutbox.Received(1).QueueForSessionAsync(
            session.Id,
            WorkerNow,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseIdle_WhenOnlyOneSessionTimestampIsStale_LeavesTheSessionOpen()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        session.RecordScan(
            kept: true,
            WorkerNow.AddMinutes(-30),
            WorkerNow.AddMinutes(-30));
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new CloseIdleScanSessionsCommandHandler(
            fixture.Context,
            clock,
            fixture.CreateCloseScanSessionHandler(WorkerNow));

        var result = await handler.Handle(
            new CloseIdleScanSessionsCommand(),
            CancellationToken.None);

        result.ClosedCount.Should().Be(0);
        (await fixture.Context.ScanSessions.SingleAsync()).Status
            .Should().Be(ScanSessionStatus.InProgress);
    }

    [Fact]
    public async Task ReleaseDue_WhenFairHasStarted_ReleasesAnnouncementAndCreatesOneFairMovement()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var fair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T18:00:00+00:00"),
            DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
            DateTimeOffset.Parse("2026-09-03T18:30:00+00:00"),
            null);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair, fair.Id);
        fixture.Context.BookAnnouncements.Add(BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            book.Isbn13,
            fair.Id,
            quantity: 2,
            ScanBookCommandHandlerTests.ClientScanAt,
            session.Id));
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new ReleaseDueAnnouncementsCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new ReleaseDueAnnouncementsCommand(),
            CancellationToken.None);

        result.ReleasedCount.Should().Be(1);
        result.ReleasedQuantity.Should().Be(2);
        var announcement = await fixture.Context.BookAnnouncements.SingleAsync();
        announcement.Status.Should().Be(BookAnnouncementStatus.Released);
        announcement.ReleasedAt.Should().Be(WorkerNow);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(2);
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.FairRelease);
        movement.Quantity.Should().Be(2);
        movement.ScanSessionId.Should().BeNull();
        movement.VolunteerId.Should().BeNull();
        movement.AssoEventsId.Should().Be(fair.Id);

        var retry = await handler.Handle(
            new ReleaseDueAnnouncementsCommand(),
            CancellationToken.None);

        retry.ReleasedCount.Should().Be(0);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(2);
    }

    [Fact]
    public async Task ReleaseDue_WhenFairHasNotStartedOrIsNotABookFair_LeavesAnnouncementsUntouched()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.NextFair);
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var futureFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
            null,
            DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
            null);
        var otherEventId = await fixture.AddOtherEventAsync();
        fixture.Context.BookAnnouncements.AddRange(
            BookAnnouncement.Create(
                BookAnnouncementId.CreateUnique(),
                book.Isbn13,
                futureFair.Id,
                1,
                ScanBookCommandHandlerTests.ClientScanAt,
                session.Id),
            BookAnnouncement.Create(
                BookAnnouncementId.CreateUnique(),
                book.Isbn13,
                otherEventId,
                1,
                ScanBookCommandHandlerTests.ClientScanAt,
                session.Id));
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new ReleaseDueAnnouncementsCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new ReleaseDueAnnouncementsCommand(),
            CancellationToken.None);

        result.ReleasedCount.Should().Be(0);
        (await fixture.Context.BookAnnouncements
                .AllAsync(announcement => announcement.Status == BookAnnouncementStatus.Announced))
            .Should().BeTrue();
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AttachNextFair_WhenSeveralFairsExist_UsesTheNearestFutureBooksFair()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.NextFair);
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var nearestFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
            null,
            DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
            null);
        var laterFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-05T18:00:00+00:00"),
            null,
            DateTimeOffset.Parse("2026-09-05T18:00:00+00:00"),
            null);
        var announcement = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            book.Isbn13,
            assoEventsId: null,
            1,
            ScanBookCommandHandlerTests.ClientScanAt,
            session.Id);
        fixture.Context.BookAnnouncements.Add(announcement);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new AttachUndatedAnnouncementsToNextFairCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new AttachUndatedAnnouncementsToNextFairCommand(),
            CancellationToken.None);

        result.AttachedCount.Should().Be(1);
        result.TargetFairId.Should().Be(nearestFair.Id);
        announcement.AssoEventsId.Should().Be(nearestFair.Id);
        announcement.AssoEventsId.Should().NotBe(laterFair.Id);
    }

    [Fact]
    public async Task AttachNextFair_WhenAttachedAnnouncementBelongsToCancelledFair_ReassignsItToTheNextFair()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.NextFair);
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var cancelledFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T18:00:00+00:00"),
            null,
            DateTimeOffset.Parse("2026-09-03T18:00:00+00:00"),
            null);
        cancelledFair.Cancel().Should().BeTrue();
        var nextFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
            null,
            DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
            null);
        var announcement = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            book.Isbn13,
            cancelledFair.Id,
            1,
            ScanBookCommandHandlerTests.ClientScanAt,
            session.Id);
        fixture.Context.BookAnnouncements.Add(announcement);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new AttachUndatedAnnouncementsToNextFairCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new AttachUndatedAnnouncementsToNextFairCommand(),
            CancellationToken.None);

        result.DetachedCount.Should().Be(1);
        result.AttachedCount.Should().Be(1);
        result.TargetFairId.Should().Be(nextFair.Id);
        announcement.AssoEventsId.Should().Be(nextFair.Id);
    }

    [Fact]
    public async Task EnrichPending_WhenResolverFindsMetadata_AppliesItAndDoesNotCallItAgain()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>()).Returns(
            new BookMetadataResult(
                book.Isbn13.Value,
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                "Gallimard",
                1946,
                null,
                "BnF",
                "OL42W",
                new DateTimeOffset(2026, 9, 3, 20, 0, 0, TimeSpan.Zero)));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new EnrichPendingBooksCommandHandler(fixture.Context, resolver, clock);

        var result = await handler.Handle(
            new EnrichPendingBooksCommand(),
            CancellationToken.None);

        result.ResolvedCount.Should().Be(1);
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.MetadataStatus.Should().Be(BookMetadataStatus.Resolved);
        persisted.Title.Should().Be("Le Petit Prince");
        persisted.WorkId.Should().Be("OL42W");
        persisted.ResolveAttempts.Should().Be(1);

        var retry = await handler.Handle(
            new EnrichPendingBooksCommand(),
            CancellationToken.None);

        retry.ProcessedCount.Should().Be(0);
        await resolver.Received(1).ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichPending_WhenCoverIsAvailable_StoresTheBlobReferenceWithTheMetadata()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>()).Returns(
            new BookMetadataResult(
                book.Isbn13.Value,
                "Le Petit Prince",
                null,
                null,
                null,
                new Uri("https://covers.openlibrary.org/b/id/42-L.jpg"),
                "OpenLibrary",
                null,
                new DateTimeOffset(2026, 9, 3, 20, 0, 0, TimeSpan.Zero)));
        var coverStorage = Substitute.For<IBookCoverStorage>();
        var coverReference = new Uri("https://storage.example.test/book-covers/42.jpg");
        coverStorage.TryStoreAsync(
                book.Isbn13,
                Arg.Any<Uri>(),
                Arg.Any<CancellationToken>())
            .Returns(coverReference);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new EnrichPendingBooksCommandHandler(
            fixture.Context,
            resolver,
            clock,
            coverStorage);

        var result = await handler.Handle(
            new EnrichPendingBooksCommand(),
            CancellationToken.None);

        result.ResolvedCount.Should().Be(1);
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.MetadataStatus.Should().Be(BookMetadataStatus.Resolved);
        persisted.CoverBlobRef.Should().Be(coverReference.ToString());
        await coverStorage.Received(1).TryStoreAsync(
            book.Isbn13,
            Arg.Any<Uri>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichPending_WhenCoverProviderFails_LeavesBibliographicMetadataPendingForReplay()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>()).Returns(
            new BookMetadataResult(
                book.Isbn13.Value,
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                "Gallimard",
                1946,
                new Uri("https://covers.openlibrary.org/b/id/42-L.jpg"),
                "BnF",
                "OL42W",
                new DateTimeOffset(2026, 9, 3, 20, 0, 0, TimeSpan.Zero)));
        var coverStorage = Substitute.For<IBookCoverStorage>();
        coverStorage.TryStoreAsync(
                book.Isbn13,
                Arg.Any<Uri>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Uri?>(new HttpRequestException("cover provider unavailable")));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new EnrichPendingBooksCommandHandler(
            fixture.Context,
            resolver,
            clock,
            coverStorage);

        var result = await handler.Handle(
            new EnrichPendingBooksCommand(),
            CancellationToken.None);

        result.FailedCount.Should().Be(1);
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.MetadataStatus.Should().Be(BookMetadataStatus.Pending);
        persisted.Title.Should().BeNull();
        persisted.ResolveAttempts.Should().Be(0);
    }

    [Fact]
    public async Task EnrichPending_WhenProvidersFail_LeavesTheBookPendingForAReplay()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BookMetadataResult?>(
                new HttpRequestException("providers unavailable")));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new EnrichPendingBooksCommandHandler(fixture.Context, resolver, clock);

        var result = await handler.Handle(
            new EnrichPendingBooksCommand(),
            CancellationToken.None);

        result.FailedCount.Should().Be(1);
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.MetadataStatus.Should().Be(BookMetadataStatus.Pending);
        persisted.ResolveAttempts.Should().Be(0);
    }

    [Fact]
    public async Task EnrichPending_WhenResolverFindsNothing_StoresNegativeCacheAndHonorsRetryWindow()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>())
            .Returns((BookMetadataResult?)null);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new EnrichPendingBooksCommandHandler(fixture.Context, resolver, clock);

        var first = await handler.Handle(
            new EnrichPendingBooksCommand(),
            CancellationToken.None);
        var second = await handler.Handle(
            new EnrichPendingBooksCommand(),
            CancellationToken.None);

        first.NotFoundCount.Should().Be(1);
        second.ProcessedCount.Should().Be(0);
        (await fixture.Context.Books.SingleAsync()).MetadataStatus
            .Should().Be(BookMetadataStatus.NotFound);
        await resolver.Received(1).ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>());
    }
}
