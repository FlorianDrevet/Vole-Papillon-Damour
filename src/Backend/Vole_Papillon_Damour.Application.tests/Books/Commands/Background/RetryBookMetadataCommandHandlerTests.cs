using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Background;

public sealed class RetryBookMetadataCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Handle_WhenBookIsNotFound_ResetsItAndQueuesAnImmediateRetry()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        book.RecordMetadataNotFound(ScanBookCommandHandlerTests.ReceivedAt);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt.AddMinutes(1));
        var queue = Substitute.For<IBookMetadataEnrichmentQueue>();
        var handler = new RetryBookMetadataCommandHandler(fixture.Context, clock, queue);

        var result = await handler.Handle(
            new RetryBookMetadataCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.MetadataStatus.Should().Be(BookMetadataStatus.Pending);
        persisted.LastAttemptAt.Should().BeNull();
        persisted.MetadataFailureReason.Should().BeNull();
        queue.Received(1).Enqueue(book.Isbn13);
    }
}
