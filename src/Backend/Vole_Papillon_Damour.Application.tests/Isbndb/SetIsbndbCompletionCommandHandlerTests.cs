using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb.Commands.SetIsbndbCompletion;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class SetIsbndbCompletionCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Administrator = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000123"));

    [Fact]
    public async Task Handle_WhenCompletedBookHasTitleAndAuthor_RecordsAdministratorAndRelaunchesRecommendation()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch("Titre", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-1),
            null);
        book.MarkAbsentFromIsbndb(Now.AddDays(-1));
        fixture.Context.Books.Add(book);
        var job = BookRecommendationJob.Create(isbn13.Value);
        job.Request(Now.AddDays(-1));
        fixture.Context.BookRecommendationJobs.Add(job);
        await fixture.Context.SaveChangesAsync();

        var queue = Substitute.For<IBookRecommendationWorkQueue>();
        var handler = CreateHandler(fixture, queue);
        var result = await handler.Handle(
            new SetIsbndbCompletionCommand(isbn13.Value, true, Administrator),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.IsCompleted.Should().BeTrue();
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.IsbndbCompletedAt.Should().Be(Now);
        persisted.IsbndbCompletedBy.Should().Be(Administrator);
        (await fixture.Context.BookRecommendationJobs.SingleAsync()).RequestVersion.Should().Be(2);
        await queue.Received(1).EnqueueBooksAsync(
            Arg.Is<IReadOnlyCollection<BookRecommendationQueueItem>>(items => items.Single().Isbn13 == isbn13.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTitleOrAuthorIsMissing_ReturnsValidationError()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.MarkAbsentFromIsbndb(Now.AddDays(-1));
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        var handler = CreateHandler(fixture, null);

        var result = await handler.Handle(
            new SetIsbndbCompletionCommand(isbn13.Value, true, Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.IsbndbCompletionIncomplete");
        (await fixture.Context.Books.SingleAsync()).IsbndbCompletedAt.Should().BeNull();
    }

    private static SetIsbndbCompletionCommandHandler CreateHandler(
        ScanBookFixture fixture,
        IBookRecommendationWorkQueue? queue)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new SetIsbndbCompletionCommandHandler(fixture.Context, clock, queue);
    }
}
