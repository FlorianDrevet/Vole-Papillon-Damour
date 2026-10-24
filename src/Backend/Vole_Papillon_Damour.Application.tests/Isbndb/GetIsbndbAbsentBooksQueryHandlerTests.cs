using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbAbsentBooks;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class GetIsbndbAbsentBooksQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ToComplete_FiltersSearchesAndSortsAvailableStockFirst()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var stock = CreateAbsentBook("9782070363735", Now.AddDays(-20), "Les Misérables", "Victor Hugo", "Bnf");
        stock.RecordAvailableEntry(Now.AddDays(-1), quantity: 2);
        var newest = CreateAbsentBook("9782070409228", Now.AddDays(-1), "Germinal", "Émile Zola", "OpenLibrary");
        var completed = CreateAbsentBook("9783140464079", Now.AddDays(-3), "Dune", "Frank Herbert", "GoogleBooks");
        completed.MarkIsbndbCompleted(
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000123")),
            Now.AddDays(-1));
        fixture.Context.Books.AddRange(stock, newest, completed);
        await fixture.Context.SaveChangesAsync();

        var handler = new GetIsbndbAbsentBooksQueryHandler(fixture.Context);
        var result = await handler.Handle(
            new GetIsbndbAbsentBooksQuery("to-complete", "9782070363735", 1, 20),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Isbn13.Should().Be(stock.Id.Value);
        result.Value.Items[0].FallbackSource.Should().Be("Bnf");
        result.Value.Items[0].QuantityAvailable.Should().Be(2);
        result.Value.Items[0].MissingFields.Should().Contain(["Summary", "CoverUrl", "Genre", "Publisher", "PublicationYear", "Pages"]);

        var completedResult = await handler.Handle(
            new GetIsbndbAbsentBooksQuery("completed", null, 1, 20),
            CancellationToken.None);
        completedResult.Value.Items.Should().ContainSingle(item => item.Isbn13 == completed.Id.Value);
    }

    private static Book CreateAbsentBook(string isbn, DateTime firstSeenAt, string title, string authors, string source)
    {
        Isbn13.TryCreate(isbn, out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, firstSeenAt);
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(title, authors, null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            Enum.Parse<BookMetadataSource>(source),
            firstSeenAt,
            null);
        book.MarkAbsentFromIsbndb(firstSeenAt);
        return book;
    }
}
