using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetBookMetadataQueueQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPendingFailuresAndIncompleteTitlesButNotCompletedOrDiscardedBooks()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var pending = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var failed = await fixture.AddBookAsync("9782070408504", quantityAvailable: 1);
        failed.RecordMetadataProviderFailure(
            ScanBookCommandHandlerTests.ReceivedAt,
            "Le fournisseur de notices est indisponible.");
        var completed = await fixture.AddBookAsync("9783140464079", quantityAvailable: 1);
        completed.ApplyManualMetadata(
            new BookMetadataPatch("Livre complet", null, null, null, null, null, null, null,
                [BookMetadataField.Title]),
            ScanBookCommandHandlerTests.ReceivedAt);
        var discarded = await fixture.AddBookAsync("9782253006329", quantityAvailable: 1);
        discarded.DiscardMetadataCandidate(ScanBookCommandHandlerTests.ReceivedAt);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetBookMetadataQueueQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetBookMetadataQueueQuery(Page: 1, PageSize: 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(item => item.Isbn13)
            .Should().BeEquivalentTo(pending.Isbn13.Value, failed.Isbn13.Value);
        result.Value.Items.Single(item => item.Isbn13 == failed.Isbn13.Value)
            .FailureReason.Should().Be("Le fournisseur de notices est indisponible.");
    }

    [Fact]
    public async Task Handle_UsesSqlPagingForTheQueue()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        await fixture.AddBookAsync("9782070408504", quantityAvailable: 0);
        await fixture.AddBookAsync("9783140464079", quantityAvailable: 0);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetBookMetadataQueueQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetBookMetadataQueueQuery(Page: 2, PageSize: 1),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
        result.Value.Items.Should().ContainSingle();
    }

    [Theory]
    [InlineData(BookMetadataStatus.Pending, 1)]
    [InlineData(BookMetadataStatus.Failed, 2)]
    public async Task Handle_FiltersByMetadataStatusBeforePaging(
        BookMetadataStatus requestedStatus,
        int expectedTotalCount)
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var failed = await fixture.AddBookAsync("9782070408504", quantityAvailable: 1);
        failed.RecordMetadataProviderFailure(
            ScanBookCommandHandlerTests.ReceivedAt,
            "Le fournisseur de notices est indisponible.");
        var anotherFailed = await fixture.AddBookAsync("9783140464079", quantityAvailable: 1);
        anotherFailed.RecordMetadataProviderFailure(
            ScanBookCommandHandlerTests.ReceivedAt,
            "Aucune notice bibliographique exploitable.");
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new GetBookMetadataQueueQueryHandler(fixture.Context, clock);
        var query = new GetBookMetadataQueueQuery(
            Page: 1,
            PageSize: 1,
            MetadataStatus: requestedStatus);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(expectedTotalCount);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].MetadataStatus.Should().Be(requestedStatus.ToString());
    }
}
