using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.UpdateBookMetadata;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
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

    private static UpdateBookMetadataCommandHandler CreateHandler(ScanBookFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(UpdatedAt);
        return new UpdateBookMetadataCommandHandler(fixture.Context, clock);
    }
}
