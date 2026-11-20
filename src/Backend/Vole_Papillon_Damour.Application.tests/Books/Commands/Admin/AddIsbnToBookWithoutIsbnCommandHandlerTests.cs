using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Admin;

public sealed class AddIsbnToBookWithoutIsbnCommandHandlerTests
{
    private const string InternalCode = "2000000000015";
    private const string Isbn = "9782070363735";
    private static readonly UserId Administrator = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Handle_MergesTheStockIntoTheExistingIsbnFiche_AndKeepsTheInternalCodeResolvable()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddInternalFicheAsync(fixture, quantity: 3);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 1);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, Isbn, Administrator),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be(Isbn);
        result.Value.QuantityAvailable.Should().Be(4);

        // RG-174 : le code interne reste valide et résout vers la fiche ISBN, qui porte le stock.
        var source = await fixture.Context.Books.SingleAsync(book => book.Id == BookCode.CreateInternal(InternalCode));
        source.RedirectedToIsbn13.Should().NotBeNull();
        source.RedirectedToIsbn13!.Value.Value.Should().Be(Isbn);
        source.IsHiddenFromCatalog.Should().BeTrue();
        source.QuantityAvailable.Should().Be(0);

        var movements = await fixture.Context.BookMovements.ToListAsync();
        movements.Should().Contain(movement =>
            movement.Isbn13.Value == InternalCode && movement.Quantity == -3 && movement.Type == BookMovementType.Correction);
        movements.Should().Contain(movement =>
            movement.Isbn13.Value == Isbn && movement.Quantity == 3 && movement.Type == BookMovementType.Correction);
    }

    [Fact]
    public async Task Handle_CreatesTheIsbnFicheWithTheManuallyEditedMetadata_WhenNoIsbnFicheExists()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddInternalFicheAsync(fixture, quantity: 2);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, Isbn, Administrator),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var isbnFiche = await fixture.Context.Books.SingleAsync(book => book.Id == IsbnCode());
        isbnFiche.IsInternalCode.Should().BeFalse();
        isbnFiche.Title.Should().Be("Atlas du Maroc");
        isbnFiche.Authors.Should().Be("Anonyme");
        isbnFiche.Publisher.Should().Be("Éditions Test");
        isbnFiche.PublicationYear.Should().Be(1999);
        isbnFiche.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        isbnFiche.IsMetadataFieldManuallyEdited(BookMetadataField.Title).Should().BeTrue();
        isbnFiche.IsMetadataFieldManuallyEdited(BookMetadataField.Authors).Should().BeTrue();
        isbnFiche.QuantityAvailable.Should().Be(2);

        var source = await fixture.Context.Books.SingleAsync(book => book.Id == BookCode.CreateInternal(InternalCode));
        source.RedirectedToIsbn13!.Value.Value.Should().Be(Isbn);
        result.Value.QuantityAvailable.Should().Be(2);
    }

    [Fact]
    public async Task Handle_RefusesAnInternalCodeAsTheIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddInternalFicheAsync(fixture, quantity: 1);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, "2000000000022", Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidIsbn");
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_RefusesAnInvalidIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddInternalFicheAsync(fixture, quantity: 1);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, "9782070363736", Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidIsbn");
    }

    [Fact]
    public async Task Handle_AcceptsAnIsbn10AndConvertsIt()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddInternalFicheAsync(fixture, quantity: 1);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, "2070363732", Administrator),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be(Isbn);
    }

    [Fact]
    public async Task Handle_RefusesAnIsbnFicheAsTheSource()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync(Isbn, quantityAvailable: 1);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(Isbn, "9780306406157", Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.NotAnInternalCode");
    }

    [Fact]
    public async Task Handle_RefusesAnUnknownInternalCode()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, Isbn, Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.NotFound");
    }

    [Fact]
    public async Task Handle_RefusesAnExistingIsbnFicheThatIsAlreadyRedirected()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddInternalFicheAsync(fixture, quantity: 1);
        var redirected = await fixture.AddBookAsync(Isbn, quantityAvailable: 0);
        redirected.RedirectTo(Isbn13FromCode("9780306406157"));
        await fixture.Context.SaveChangesAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, Isbn, Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.CannotMerge");
        var source = await fixture.Context.Books.SingleAsync(book => book.Id == BookCode.CreateInternal(InternalCode));
        source.RedirectedToIsbn13.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RefusesWhileTheInternalFicheHasActiveAnnouncements()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddInternalFicheAsync(fixture, quantity: 0);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair);
        fixture.Context.BookAnnouncements.Add(BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            BookCode.CreateInternal(InternalCode),
            assoEventsId: null,
            quantity: 1,
            ScanBookCommandHandlerTests.ClientScanAt,
            session.Id));
        await fixture.Context.SaveChangesAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new AddIsbnToBookWithoutIsbnCommand(InternalCode, Isbn, Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.CannotMerge");
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
    }

    private static Isbn13 Isbn13FromCode(string value)
    {
        Isbn13.TryCreate(value, out var isbn13).Should().BeTrue();
        return isbn13;
    }

    private static BookCode IsbnCode() => Isbn13FromCode(Isbn);

    private static async Task AddInternalFicheAsync(ScanBookFixture fixture, int quantity)
    {
        var book = Book.CreateWithInternalCode(
            BookCode.CreateInternal(InternalCode),
            ScanBookCommandHandlerTests.SessionStartedAt);
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                Title: "Atlas du Maroc",
                Authors: "Anonyme",
                Publisher: "Éditions Test",
                PublicationYear: 1999,
                PhysicalFormat: null,
                Language: null,
                Genre: null,
                CoverUrl: null,
                Fields:
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear,
                ]),
            ScanBookCommandHandlerTests.SessionStartedAt);
        for (var index = 0; index < quantity; index++)
        {
            book.RecordAvailableEntry(ScanBookCommandHandlerTests.ClientScanAt);
        }

        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
    }

    private static AddIsbnToBookWithoutIsbnCommandHandler CreateHandler(ScanBookFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        return new AddIsbnToBookWithoutIsbnCommandHandler(
            fixture.Context,
            clock,
            new NotFoundReportLapser(fixture.Context));
    }
}
