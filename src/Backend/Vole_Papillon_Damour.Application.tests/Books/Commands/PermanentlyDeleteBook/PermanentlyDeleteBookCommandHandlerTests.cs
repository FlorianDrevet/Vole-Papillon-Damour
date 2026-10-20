using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.PermanentlyDeleteBook;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.PermanentlyDeleteBook;

public sealed class PermanentlyDeleteBookCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Handle_WhenBookHasOnlyDirectStockHistory_DeletesTheBookAndItsCatalogueHistory()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var stockEntry = CreateMovement(book, BookMovementType.DirectEntry);
        var memberId = UserId.CreateUnique();
        var member = User.CreateFromExternalIdentity(
            memberId,
            "external-member-id",
            "member@example.test",
            ScanBookCommandHandlerTests.ReceivedAt);
        fixture.Context.Users.Add(member);
        fixture.Context.BookMovements.Add(stockEntry);
        fixture.Context.BookMovements.Add(CreateMovement(
            book,
            BookMovementType.Correction,
            reversalOfMovementId: stockEntry.Id));
        fixture.Context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(
            Guid.NewGuid(),
            memberId,
            book.Isbn13,
            ScanBookCommandHandlerTests.ReceivedAt));
        fixture.Context.WatchlistItems.Add(WatchlistItem.CreateEdition(
            Guid.NewGuid(),
            memberId,
            book.Isbn13,
            ScanBookCommandHandlerTests.ReceivedAt,
            title: "Le Petit Prince"));
        fixture.Context.BookNotFoundReports.Add(BookNotFoundReport.CreateForEdition(
            Guid.NewGuid(),
            memberId,
            book.Isbn13,
            comment: null,
            ScanBookCommandHandlerTests.ReceivedAt));
        fixture.Context.UserAlertHistories.Add(UserAlertHistory.Create(
            Guid.NewGuid(),
            memberId,
            book.Isbn13,
            ScanBookCommandHandlerTests.ReceivedAt));
        fixture.Context.BookSimilarityProfiles.Add(BookSimilarityProfile.Create(book.Isbn13.Value));
        fixture.Context.BookNeighbors.Add(new BookNeighbor(
            Guid.Parse("00000000-0000-0000-0000-000000000101"),
            book.Isbn13.Value,
            rank: 1,
            neighborIsbn13: "9782070408504",
            score: 0.8f,
            NeighborReason.SameAuthor));
        fixture.Context.BookNeighbors.Add(new BookNeighbor(
            Guid.Parse("00000000-0000-0000-0000-000000000102"),
            "9782070408504",
            rank: 1,
            neighborIsbn13: book.Isbn13.Value,
            score: 0.8f,
            NeighborReason.SameAuthor));
        await fixture.Context.SaveChangesAsync();
        var handler = new PermanentlyDeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new PermanentlyDeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.Books.CountAsync()).Should().Be(0);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
        (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(0);
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(0);
        (await fixture.Context.BookNotFoundReports.CountAsync()).Should().Be(0);
        (await fixture.Context.UserAlertHistories.CountAsync()).Should().Be(0);
        (await fixture.Context.BookSimilarityProfiles.CountAsync()).Should().Be(0);
        (await fixture.Context.BookNeighbors.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenBookHasSales_RefusesDeletionAndKeepsItsHistory()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        fixture.Context.BookMovements.Add(CreateMovement(book, BookMovementType.Sale));
        await fixture.Context.SaveChangesAsync();
        var handler = new PermanentlyDeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new PermanentlyDeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasSales");
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenBookHasScanHistory_RefusesDeletion()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        fixture.Context.BookMovements.Add(CreateMovement(book, BookMovementType.DirectEntry, session.Id));
        await fixture.Context.SaveChangesAsync();
        var handler = new PermanentlyDeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new PermanentlyDeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasHistory");
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenBookHasAnnouncementHistory_RefusesDeletion()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        fixture.Context.BookAnnouncements.Add(BookAnnouncement.Create(
            Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.BookAnnouncementId.CreateUnique(),
            book.Isbn13,
            null,
            1,
            ScanBookCommandHandlerTests.ReceivedAt,
            session.Id));
        await fixture.Context.SaveChangesAsync();
        var handler = new PermanentlyDeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new PermanentlyDeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasHistory");
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenBookIsTheTargetOfAnExistingMerge_RefusesDeletion()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var canonicalBook = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var sourceBook = await fixture.AddBookAsync("9782070408504", quantityAvailable: 0);
        sourceBook.RedirectTo(canonicalBook.Isbn13);
        await fixture.Context.SaveChangesAsync();
        var handler = new PermanentlyDeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new PermanentlyDeleteBookCommand(canonicalBook.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasHistory");
        (await fixture.Context.Books.CountAsync()).Should().Be(2);
    }

    private static BookMovement CreateMovement(
        Book book,
        BookMovementType type,
        ScanSessionId? scanSessionId = null,
        BookMovementId? reversalOfMovementId = null)
    {
        return BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Isbn13,
            type,
            type == BookMovementType.Sale ? -1 : 1,
            ScanBookCommandHandlerTests.ClientScanAt,
            ScanBookCommandHandlerTests.ReceivedAt,
            clockSuspect: false,
            scanSessionId,
            AdministratorId,
            assoEventsId: null,
            note: null,
            clientGestureId: Guid.NewGuid(),
            reversalOfMovementId);
    }
}
