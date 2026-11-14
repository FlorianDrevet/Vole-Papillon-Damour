using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.ProcessBookRecommendations;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class ProcessBookRecommendationsIsbndbTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenFallbackBookIsNotDefinitive_LeavesJobPendingAndWaitsForMetadata()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = await fixture.AddBookAsync(isbn13.Value, quantityAvailable: 0);
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now,
            rawPayload: null);
        var job = BookRecommendationJob.Create(isbn13.Value);
        job.Request(Now);
        fixture.Context.BookRecommendationJobs.Add(job);
        await fixture.Context.SaveChangesAsync();

        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        var embeddings = Substitute.For<IBookEmbeddingService>();
        embeddings.IsConfigured.Returns(true);
        var sender = Substitute.For<MediatR.ISender>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new ProcessBookRecommendationsCommandHandler(
            fixture.Context,
            settings,
            embeddings,
            sender,
            clock);

        var result = await handler.Handle(
            new ProcessBookRecommendationsCommand([new BookRecommendationQueueItem(isbn13.Value, 1)]),
            CancellationToken.None);

        result.Should().Be(new ProcessBookRecommendationsResult(0, 0, 0, 0, 1));
        var persistedJob = await fixture.Context.BookRecommendationJobs.SingleAsync();
        persistedJob.Status.Should().Be(BookRecommendationJobStatus.Pending);
        persistedJob.AttemptCount.Should().Be(0);
        persistedJob.StartedAt.Should().BeNull();
        await sender.DidNotReceiveWithAnyArgs().Send(default!, default);
    }

    [Fact]
    public async Task Handle_WhenFallbackFavoriteReferenceIsResolved_ProcessesItWithoutWaitingForIsbndb()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.CreateFavoriteReference(isbn13, Now.AddMinutes(-1));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now,
            rawPayload: null);
        fixture.Context.Books.Add(book);
        var job = BookRecommendationJob.Create(isbn13.Value);
        job.Request(Now);
        fixture.Context.BookRecommendationJobs.Add(job);
        await fixture.Context.SaveChangesAsync();

        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        var embeddings = Substitute.For<IBookEmbeddingService>();
        embeddings.IsConfigured.Returns(true);
        var sender = Substitute.For<MediatR.ISender>();
        sender.Send(Arg.Any<RefreshBookSimilarityProfilesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RefreshBookSimilarityProfilesResult(0, 0, 0)));
        sender.Send(Arg.Any<RecomputeBookNeighborsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RecomputeBookNeighborsResult(0, 0, 0, null)));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new ProcessBookRecommendationsCommandHandler(
            fixture.Context,
            settings,
            embeddings,
            sender,
            clock);

        var result = await handler.Handle(
            new ProcessBookRecommendationsCommand([new BookRecommendationQueueItem(isbn13.Value, 1)]),
            CancellationToken.None);

        result.Started.Should().Be(1);
        result.WaitingForMetadata.Should().Be(0);
        (await fixture.Context.BookRecommendationJobs.SingleAsync()).Status.Should().Be(BookRecommendationJobStatus.NoNotice);
    }
}
