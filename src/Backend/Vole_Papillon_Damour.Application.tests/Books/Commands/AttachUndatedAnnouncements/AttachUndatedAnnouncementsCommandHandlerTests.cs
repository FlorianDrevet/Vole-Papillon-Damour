using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.AttachUndatedAnnouncements;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.AttachUndatedAnnouncements;

public sealed class AttachUndatedAnnouncementsCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenBooksFairExists_AttachesOnlyActiveUndatedAnnouncements()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var sourceSession = await fixture.AddSessionAsync(ScanMode.NextFair);
        var firstBook = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var secondBook = await fixture.AddBookAsync("9783140464079", quantityAvailable: 0);
        var datedFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-10T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-11T00:00:00+02:00"),
            null,
            null);
        var nextFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-20T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-21T00:00:00+02:00"),
            null,
            null);
        fixture.Context.BookAnnouncements.AddRange(
            BookAnnouncement.Create(
                BookAnnouncementId.CreateUnique(),
                firstBook.Isbn13,
                assoEventsId: null,
                1,
                ScanBookCommandHandlerTests.ClientScanAt,
                sourceSession.Id),
            BookAnnouncement.Create(
                BookAnnouncementId.CreateUnique(),
                secondBook.Isbn13,
                assoEventsId: null,
                1,
                ScanBookCommandHandlerTests.ClientScanAt,
                sourceSession.Id),
            BookAnnouncement.Create(
                BookAnnouncementId.CreateUnique(),
                firstBook.Isbn13,
                datedFair.Id,
                1,
                ScanBookCommandHandlerTests.ClientScanAt,
                sourceSession.Id));
        await fixture.Context.SaveChangesAsync();
        var cancelled = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            secondBook.Isbn13,
            assoEventsId: null,
            1,
            ScanBookCommandHandlerTests.ClientScanAt,
            sourceSession.Id);
        cancelled.Cancel();
        fixture.Context.BookAnnouncements.Add(cancelled);
        await fixture.Context.SaveChangesAsync();
        var handler = fixture.CreateAttachUndatedAnnouncementsHandler();

        var result = await handler.Handle(
            new AttachUndatedAnnouncementsCommand(nextFair.Id),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AttachedCount.Should().Be(2);
        (await fixture.Context.BookAnnouncements
                .Where(announcement => announcement.Status == BookAnnouncementStatus.Announced)
                .Where(announcement => announcement.AssoEventsId == null)
                .CountAsync())
            .Should().Be(0);
        (await fixture.Context.BookAnnouncements
                .Where(announcement => announcement.AssoEventsId == nextFair.Id)
                .CountAsync())
            .Should().Be(2);
        cancelled.AssoEventsId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTargetIsNotABooksFair_RefusesAttachment()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var otherEventId = await fixture.AddOtherEventAsync();
        var handler = fixture.CreateAttachUndatedAnnouncementsHandler();

        var result = await handler.Handle(
            new AttachUndatedAnnouncementsCommand(otherEventId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.TargetFairMustBeBooks");
    }
}
