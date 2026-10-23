using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.UpdateBookMetadata;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.UpdateBookMetadata;

public sealed class UpdateBookMetadataCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    private static readonly DateTime UpdatedAt =
        new(2026, 9, 3, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_UpdatesSelectedFieldsAndStoresManualLocks()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new UpdateBookMetadataCommand(
                "9782070363735",
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                "Gallimard",
                1946,
                null,
                null,
                "Conte",
                null,
                [BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.Genre],
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be("9782070363735");
        result.Value.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        result.Value.Changed.Should().BeTrue();
        result.Value.ManuallyEditedFields.Should().Be("[\"Title\",\"Authors\",\"Genre\"]");

        var book = await fixture.Context.Books.SingleAsync();
        book.Title.Should().Be("Le Petit Prince");
        book.Authors.Should().Be("Antoine de Saint-Exupéry");
        book.Publisher.Should().BeNull();
        book.PublicationYear.Should().BeNull();
        book.Genre.Should().Be("Conte");
    }

    [Fact]
    public async Task Handle_WhenSelectedFieldIsNull_ClearsItWithoutUnlockingOtherFields()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var handler = CreateHandler(fixture);
        await handler.Handle(
            new UpdateBookMetadataCommand(
                "9782070363735",
                "Titre",
                "Auteur",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors],
                AdministratorId),
            CancellationToken.None);

        var result = await handler.Handle(
            new UpdateBookMetadataCommand(
                "9782070363735",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title],
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var book = await fixture.Context.Books.SingleAsync();
        book.Title.Should().BeNull();
        book.Authors.Should().Be("Auteur");
        book.ManuallyEditedFields.Should().Be("[\"Title\",\"Authors\"]");
    }

    [Fact]
    public async Task Handle_WhenReplacingGenre_RemovesPreviousGenreRowsFromDatabase()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                null,
                null,
                null,
                null,
                null,
                null,
                "Romans",
                null,
                [BookMetadataField.Genre],
                Genres: ["Romans"]),
            BookMetadataSource.Bnf,
            UpdatedAt.AddDays(-1),
            rawPayload: null);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new UpdateBookMetadataCommand(
                "9782070363735",
                null,
                null,
                null,
                null,
                null,
                null,
                "Conte",
                null,
                [BookMetadataField.Genre],
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var genres = await fixture.Context.Set<BookGenre>()
            .AsNoTracking()
            .Where(genre => genre.BookIsbn13 == book.Isbn13)
            .Select(genre => genre.Name)
            .ToArrayAsync();
        genres.Should().Equal("Conte");
    }

    [Fact]
    public async Task Handle_WhenNoFieldIsSelected_ReturnsValidationError()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new UpdateBookMetadataCommand(
                "9782070363735",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidMetadataFields");
    }

    [Fact]
    public async Task Handle_UpdatesIsbndbEditionFieldsAndRelauchesRecommendationForCompletedAbsentBook()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, UpdatedAt.AddDays(-2));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch("Titre", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            UpdatedAt.AddDays(-2),
            null);
        book.MarkAbsentFromIsbndb(UpdatedAt.AddDays(-1));
        book.MarkIsbndbCompleted(AdministratorId, UpdatedAt.AddDays(-1));
        fixture.Context.Books.Add(book);
        var job = BookRecommendationJob.Create(isbn13.Value);
        job.Request(UpdatedAt.AddDays(-1));
        fixture.Context.BookRecommendationJobs.Add(job);
        await fixture.Context.SaveChangesAsync();

        var queue = Substitute.For<IBookRecommendationWorkQueue>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(UpdatedAt);
        var handler = new UpdateBookMetadataCommandHandler(fixture.Context, clock, queue);

        var result = await handler.Handle(new UpdateBookMetadataCommand(
            isbn13.Value,
            "Titre revu",
            "Auteur revu",
            null,
            null,
            null,
            null,
            "Biographie",
            null,
            [BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.Genre,
                BookMetadataField.Summary, BookMetadataField.Pages, BookMetadataField.Edition],
            AdministratorId,
            Summary: "Résumé revu",
            Pages: 224,
            Edition: "Édition revue",
            Genres: ["Biographie", "Histoire"]), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await fixture.Context.Books.Include(candidate => candidate.Genres).SingleAsync();
        updated.Pages.Should().Be(224);
        updated.Edition.Should().Be("Édition revue");
        updated.Genres.Select(genre => genre.Name).Should().BeEquivalentTo("Biographie", "Histoire");
        (await fixture.Context.BookRecommendationJobs.SingleAsync()).RequestVersion.Should().Be(2);
        await queue.Received(1).EnqueueBooksAsync(
            Arg.Is<IReadOnlyCollection<BookRecommendationQueueItem>>(items => items.Single().Isbn13 == isbn13.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RejectsGenresOutsideTheCatalogList()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var result = await CreateHandler(fixture).Handle(new UpdateBookMetadataCommand(
            "9782070363735", null, null, null, null, null, null, "Fantasy", null,
            [BookMetadataField.Genre], AdministratorId, Genres: ["Conte"]), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidMetadataValues");
    }

    private static UpdateBookMetadataCommandHandler CreateHandler(ScanBookFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(UpdatedAt);
        return new UpdateBookMetadataCommandHandler(fixture.Context, clock);
    }
}
