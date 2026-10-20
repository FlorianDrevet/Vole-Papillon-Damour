using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Admin;

public sealed class ImportAdminBooksCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenNewIsbnsAreImported_QueuesEachCreatedIsbnOnce()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var existingIsbn = ParseIsbn("9782070363735");
        var createdIsbn = ParseIsbn("9791036377426");
        await fixture.AddBookAsync(existingIsbn.Value, quantityAvailable: 1);

        var queue = Substitute.For<IBookMetadataEnrichmentQueue>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));
        var handler = new ImportAdminBooksCommandHandler(fixture.Context, clock, queue);
        var command = new ImportAdminBooksCommand(
            new[] { existingIsbn.Value, createdIsbn.Value, createdIsbn.Value },
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002")));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CreatedCount.Should().Be(1);
        result.Value.AlreadyPresentCount.Should().Be(2);
        (await fixture.Context.Books.SingleAsync(book => book.Id == createdIsbn))
            .MetadataStatus.Should().Be(BookMetadataStatus.Pending);
        queue.Received(1).Enqueue(createdIsbn);
        queue.DidNotReceive().Enqueue(existingIsbn);
    }

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}
