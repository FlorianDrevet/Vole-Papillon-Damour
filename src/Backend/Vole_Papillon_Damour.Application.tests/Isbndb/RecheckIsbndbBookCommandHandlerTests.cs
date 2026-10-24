using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Application.Isbndb.Commands.RecheckIsbndbBook;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class RecheckIsbndbBookCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenQuotaIsExhausted_ReturnsResetTimeWithoutChangingBook()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre de secours", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-1),
            null);
        book.MarkAbsentFromIsbndb(Now.AddDays(-1));
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();

        var lookups = Substitute.For<IIsbndbLookupService>();
        lookups.LookupAsync(isbn13, IsbndbUsage.Administration, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.QuotaExhausted(Now.AddHours(12)));
        var handler = CreateHandler(fixture, lookups);

        var result = await handler.Handle(new RecheckIsbndbBookCommand(isbn13.Value), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Outcome.Should().Be("QuotaExhausted");
        result.Value.ResetsAt.Should().Be(Now.AddHours(12));
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.IsAbsentFromIsbndb.Should().BeTrue();
        persisted.MetadataSource.Should().Be(BookMetadataSource.Bnf);
        await lookups.Received(1).LookupAsync(isbn13, IsbndbUsage.Administration, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIsbndbFindsBook_AppliesNoticeAndStoresRecord()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre de secours", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-1),
            null);
        book.MarkAbsentFromIsbndb(Now.AddDays(-1));
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();

        var notice = new IsbndbBook
        {
            Isbn13 = isbn13.Value,
            Title = "Titre ISBNdb",
            Authors = ["Auteur ISBNdb"],
            Publisher = "Gallimard",
            RawJson = "{\"title\":\"Titre ISBNdb\"}"
        };
        var lookups = Substitute.For<IIsbndbLookupService>();
        lookups.LookupAsync(isbn13, IsbndbUsage.Administration, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.Found(notice));
        var handler = CreateHandler(fixture, lookups);

        var result = await handler.Handle(new RecheckIsbndbBookCommand(isbn13.Value), CancellationToken.None);

        result.Value.Outcome.Should().Be("Found");
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.IsIsbndbSourced.Should().BeTrue();
        persisted.IsAbsentFromIsbndb.Should().BeFalse();
        persisted.Title.Should().Be("Titre ISBNdb");
        (await fixture.Context.BookIsbndbRecords.SingleAsync()).RawJson.Should().Be(notice.RawJson);
    }

    [Fact]
    public async Task Handle_WhenIsbndbDoesNotFindBook_RecordsCheckWithoutChangingFallbackMetadata()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre de secours", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-1),
            null);
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        var lookups = Substitute.For<IIsbndbLookupService>();
        lookups.LookupAsync(isbn13, IsbndbUsage.Administration, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.NotFound());

        var result = await CreateHandler(fixture, lookups).Handle(
            new RecheckIsbndbBookCommand(isbn13.Value), CancellationToken.None);

        result.Value.Outcome.Should().Be("NotFound");
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.IsAbsentFromIsbndb.Should().BeTrue();
        persisted.Title.Should().Be("Livre de secours");
        persisted.MetadataSource.Should().Be(BookMetadataSource.Bnf);
        persisted.IsbndbCheckedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_DoesNotOverwriteACompletedBook()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch("Titre final", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-1),
            null);
        book.MarkAbsentFromIsbndb(Now.AddDays(-1));
        book.MarkIsbndbCompleted(
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000123")),
            Now.AddDays(-1));
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        var lookups = Substitute.For<IIsbndbLookupService>();

        var result = await CreateHandler(fixture, lookups).Handle(
            new RecheckIsbndbBookCommand(isbn13.Value), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Isbndb.BookCompleted");
        await lookups.DidNotReceive().LookupAsync(
            Arg.Any<Isbn13>(), Arg.Any<IsbndbUsage>(), Arg.Any<CancellationToken>());
    }

    private static RecheckIsbndbBookCommandHandler CreateHandler(
        ScanBookFixture fixture,
        IIsbndbLookupService lookupService)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new RecheckIsbndbBookCommandHandler(fixture.Context, lookupService, clock);
    }
}
