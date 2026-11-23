using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminBooksQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenPageIsRequested_ReturnsOnlyThatPageAndKeepsTheTotalCount()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var books = new[]
        {
            await fixture.AddBookAsync("9782070363735", quantityAvailable: 0),
            await fixture.AddBookAsync("9782070408504", quantityAvailable: 0),
            await fixture.AddBookAsync("9783140464079", quantityAvailable: 0),
        };
        SetTitle(books[0], "Livre un");
        SetTitle(books[1], "Livre deux");
        SetTitle(books[2], "Livre trois");
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetAdminBooksQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminBooksQuery(null, null, null, null, 2, 1),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(1);
        result.Value.Books.Should().ContainSingle().Which.Isbn13.Should().Be("9782070408504");
    }

    [Fact]
    public async Task Handle_UsesFirstPhotoAsCoverForBookWithoutIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var code = BookCode.CreateInternal("2000000000015");
        var book = Book.CreateWithInternalCode(code, ScanBookCommandHandlerTests.ReceivedAt);
        SetTitle(book, "Livre sans ISBN");
        book.RecordAvailableEntry(ScanBookCommandHandlerTests.ReceivedAt);
        book.AddPhoto(
            BookPhoto.Create(
                Guid.Parse("d7972e91-4dc1-48d0-97d4-84ea0867988b"),
                code,
                new Uri("https://blob.test/sans-isbn/2000000000015/cover.jpg"),
                "sans-isbn/2000000000015/cover.jpg",
                0,
                "image/jpeg",
                1024,
                ScanBookCommandHandlerTests.ReceivedAt,
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000042"))),
            ScanBookCommandHandlerTests.ReceivedAt);
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);

        var result = await new GetAdminBooksQueryHandler(fixture.Context, clock).Handle(
            new GetAdminBooksQuery(null, null, null, null, 1, 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle().Which.CoverUrl
            .Should().Be("https://blob.test/sans-isbn/2000000000015/cover.jpg");
    }

    [Fact]
    public async Task Handle_WhenUndatedFilterIsEnabled_ReturnsOnlyBooksWithUndatedAnnouncements()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair);
        var scan = await fixture.CreateHandler().Handle(
            new ScanBookCommand(
                session.Id,
                "9782070408504",
                Kept: true,
                OccurredAt: ScanBookCommandHandlerTests.ClientScanAt,
                ClientGestureId: Guid.NewGuid(),
                VolunteerId: session.VolunteerId),
            CancellationToken.None);
        Isbn13.TryCreate("9782070408504", out var scannedIsbn).Should().BeTrue();
        var scannedBook = await fixture.Context.Books.SingleAsync(book => book.Id == scannedIsbn);
        SetTitle(scannedBook, "Livre scanné");
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetAdminBooksQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminBooksQuery(null, null, null, null, 1, 50, Undated: true),
            CancellationToken.None);

        scan.IsError.Should().BeFalse();
        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Books.Should().ContainSingle().Which.Isbn13.Should().Be("9782070408504");
    }

    [Theory]
    [InlineData("petit", "9782070408504")]
    [InlineData("207036", "9782070363735")]
    [InlineData("%", null)]
    public async Task Handle_WhenSearchIsProvided_FiltersTitleOrPartialIsbnAndEscapesWildcards(
        string search,
        string? expectedIsbn)
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var petitPrince = await fixture.AddBookAsync("9782070408504", quantityAvailable: 1);
        var second = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var third = await fixture.AddBookAsync("9783140464079", quantityAvailable: 1);
        SetTitle(petitPrince, "Le Petit Prince");
        SetTitle(second, "Autre livre");
        SetTitle(third, "Troisième livre");
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetAdminBooksQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminBooksQuery(search, null, null, null, 1, 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        if (expectedIsbn is null)
        {
            result.Value.TotalCount.Should().Be(0);
            result.Value.Books.Should().BeEmpty();
        }
        else
        {
            result.Value.TotalCount.Should().Be(1);
            result.Value.Books.Should().ContainSingle().Which.Isbn13.Should().Be(expectedIsbn);
        }
    }

    [Fact]
    public async Task Handle_ExcludesPendingBooksFromTheGeneralAdminCatalogue()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var complete = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        SetTitle(complete, "Livre complet");
        await fixture.AddBookAsync("9782070408504", quantityAvailable: 1);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetAdminBooksQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminBooksQuery(null, null, null, null, 1, 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Books.Should().ContainSingle().Which.Isbn13.Should().Be("9782070363735");
    }

    [Theory]
    [InlineData("Manual", 1)]
    [InlineData("Pending", 0)]
    public async Task Handle_UsesEnumMetadataFilterAndKeepsThePagedSqlProjectionLight(
        string metadataStatus,
        int expectedTotalCount)
    {
        var sql = new SqlCaptureInterceptor();
        await using var fixture = await ScanBookFixture.CreateAsync(sql);
        var manual = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        SetTitle(manual, "Livre manuel");
        await fixture.AddBookAsync("9782070408504", quantityAvailable: 1);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetAdminBooksQueryHandler(fixture.Context, clock);

        sql.Clear();
        var result = await handler.Handle(
            new GetAdminBooksQuery(null, metadataStatus, null, null, 1, 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(expectedTotalCount);
        if (metadataStatus == "Manual")
        {
            result.Value.Books.Should().ContainSingle().Which.Isbn13.Should().Be(manual.Id.Value);
        }
        else
        {
            result.Value.Books.Should().BeEmpty();
        }
        var pageSql = sql.Commands.Single(command => command.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
        pageSql.Should().Contain("MetadataStatus");
        pageSql.Should().NotContain("CAST");
        pageSql.Should().NotContain("RawPayload");
        pageSql.Should().NotContain("Summary");
        pageSql.Should().NotContain("ManuallyEditedFields");
        pageSql.Should().NotContain(" IS NULL OR ");
    }

    [Fact]
    public async Task Handle_RejectsUnknownMetadataStatusBeforeQuerying()
    {
        var sql = new SqlCaptureInterceptor();
        await using var fixture = await ScanBookFixture.CreateAsync(sql);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetAdminBooksQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminBooksQuery(null, "Retired", null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidMetadataStatus");
        sql.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PreservesSubjectsFromFallbackPayloadAfterProjectingThePage()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre avec sujet", null, null, null, null, null, null, null,
                [BookMetadataField.Title]),
            BookMetadataSource.OpenLibrary,
            ScanBookCommandHandlerTests.ReceivedAt,
            "{\"subjects\":[\"Robinsonades\"]}");
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);

        var result = await new GetAdminBooksQueryHandler(fixture.Context, clock).Handle(
            new GetAdminBooksQuery(null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle().Which.Subjects
            .Should().ContainSingle().Which.Should().Be("Robinsonades");
    }

    private static void SetTitle(Book book, string title)
    {
        book.ApplyManualMetadata(
            new BookMetadataPatch(title, null, null, null, null, null, null, null,
                [BookMetadataField.Title]),
            ScanBookCommandHandlerTests.ReceivedAt);
    }
}
