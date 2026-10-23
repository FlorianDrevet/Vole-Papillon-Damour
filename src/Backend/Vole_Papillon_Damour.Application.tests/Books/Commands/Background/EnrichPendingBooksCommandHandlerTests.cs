using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Background;

public sealed class EnrichPendingBooksCommandHandlerTests
{
    private static readonly DateTime WorkerNow =
        new(2026, 9, 3, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenProviderMetadataViolatesDomainConstraint_RecordsValidationDetail()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(book.Isbn13, Arg.Any<CancellationToken>()).Returns(
            new BookMetadataResult(
                book.Isbn13.Value,
                new string('x', 501),
                null,
                null,
                null,
                null,
                "BnF",
                null,
                new DateTimeOffset(WorkerNow)));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        var handler = new EnrichPendingBooksCommandHandler(fixture.Context, resolver, clock);

        await handler.Handle(
            new EnrichPendingBooksCommand(BatchSize: 1),
            CancellationToken.None);

        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.MetadataStatus.Should().Be(BookMetadataStatus.Failed);
        persisted.MetadataFailureReason.Should()
            .Contain("The metadata field cannot exceed 500 characters")
            .And.Contain("Title");
    }
}
