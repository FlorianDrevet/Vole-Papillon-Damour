using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using NSubstitute;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class RecomputeBookNeighborsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_PutsTheNextTomeFirstAndStoresItsReason()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddEditionAsync(Edition("9782742793099", "Saga 1", series: "Saga", tome: 1, author: "Auteur"), Now);
        await fixture.AddEditionAsync(Edition("9782742765010", "Saga 2", series: "Saga", tome: 2, author: "Auteur"), Now);
        await fixture.AddEditionAsync(Edition("9782253157533", "Livre voisin", author: "Autrice"), Now);
        var embeddings = new CapturingEmbeddingService(text => text.StartsWith("Saga 1", StringComparison.Ordinal)
            ? Vector(1f, 0f)
            : text.StartsWith("Saga 2", StringComparison.Ordinal)
                ? Vector(0f, 1f)
                : Vector(0.1f, 0.995f));
        var writer = new CapturingNeighborWriter();
        var handler = CreateHandler(fixture.Context, embeddings, writer, enabled: true);

        var result = await handler.Handle(new RecomputeBookNeighborsCommand(), CancellationToken.None);

        result.Books.Should().Be(3);
        result.Embedded.Should().Be(3);
        result.Neighbors.Should().Be(6);
        var firstTomeNeighbor = writer.Rows.Single(row => row.Isbn13 == "9782742793099" && row.Rank == 1);
        firstTomeNeighbor.NeighborIsbn13.Should().Be("9782742765010");
        firstTomeNeighbor.Reason.Should().Be(NeighborReason.NextTome);
        writer.BookCount.Should().Be(3);
        writer.GenerationId.Should().Be(result.GenerationId);
    }

    [Fact]
    public async Task Handle_DoesNotReembedUnchangedProfiles()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddEditionAsync(Edition("9782742793099", "Saga 1", series: "Saga", tome: 1), Now);
        await fixture.AddEditionAsync(Edition("9782742765010", "Saga 2", series: "Saga", tome: 2), Now);
        var embeddings = new CapturingEmbeddingService(_ => Vector(1f, 0f));
        var writer = new CapturingNeighborWriter();
        var handler = CreateHandler(fixture.Context, embeddings, writer, enabled: true);

        await handler.Handle(new RecomputeBookNeighborsCommand(), CancellationToken.None);
        var secondRun = await handler.Handle(new RecomputeBookNeighborsCommand(), CancellationToken.None);

        embeddings.Calls.Should().ContainSingle();
        embeddings.Calls[0].Should().HaveCount(2);
        secondRun.Embedded.Should().Be(0);
        writer.GenerationsWritten.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ReembedsOnlyEditionsSharingAChangedWorkSummary()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        var workA = Edition("9782742793099", "Même œuvre", workId: "WORK-1", summary: Summary("Résumé initial."));
        var workB = Edition("9782742765010", "Même œuvre", workId: "WORK-1");
        var otherWork = Edition("9782253157533", "Autre œuvre", workId: "WORK-2");
        await fixture.AddEditionAsync(workA, Now);
        await fixture.AddEditionAsync(workB, Now);
        await fixture.AddEditionAsync(otherWork, Now);
        var embeddings = new CapturingEmbeddingService(_ => Vector(1f, 0f));
        var writer = new CapturingNeighborWriter();
        var handler = CreateHandler(fixture.Context, embeddings, writer, enabled: true);
        await handler.Handle(new RecomputeBookNeighborsCommand(), CancellationToken.None);

        var profile = await fixture.Context.BookSimilarityProfiles.FindAsync("9782742793099");
        profile!.RecordNotice(JsonSerializer.Serialize(workA with { EditionSummary = Summary("Résumé modifié."), }), true, Now.AddDays(1));
        await fixture.Context.SaveChangesAsync();

        var result = await handler.Handle(new RecomputeBookNeighborsCommand(), CancellationToken.None);

        result.Embedded.Should().Be(2);
        embeddings.Calls.Should().HaveCount(2);
        embeddings.Calls[1].Should().HaveCount(2);
        embeddings.Calls[1].Should().OnlyContain(text => text.Contains("Résumé modifié.", StringComparison.Ordinal));
        embeddings.Calls[1].Should().OnlyContain(text => text.Contains("Même œuvre", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_WhenEmbeddingIsNotConfigured_DoesNotEmbedOrWriteGeneration()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddEditionAsync(Edition("9782742793099", "Saga 1"), Now);
        var embeddings = new CapturingEmbeddingService(_ => Vector(1f, 0f), isConfigured: false);
        var writer = new CapturingNeighborWriter();
        var handler = CreateHandler(fixture.Context, embeddings, writer, enabled: true);

        var result = await handler.Handle(new RecomputeBookNeighborsCommand(), CancellationToken.None);

        result.Should().Be(new RecomputeBookNeighborsResult(0, 0, 0, null));
        embeddings.Calls.Should().BeEmpty();
        writer.GenerationsWritten.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SendsOnlyComposedNoticeTextToEmbeddingService()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        var edition = Edition("9782070363735", "Notice exclusive", summary: Summary("Résumé bibliographique."));
        await fixture.AddEditionAsync(edition, Now);
        var profile = await fixture.Context.BookSimilarityProfiles.SingleAsync();
        var privateMarker = Guid.NewGuid().ToString();
        var privateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(privateMarker))).ToLowerInvariant();
        profile.SetProfileText(privateMarker, privateHash);
        await fixture.Context.SaveChangesAsync();

        var embeddings = new CapturingEmbeddingService(_ => Vector(1f, 0f));
        var writer = new CapturingNeighborWriter();
        var handler = CreateHandler(fixture.Context, embeddings, writer, enabled: true);

        await handler.Handle(new RecomputeBookNeighborsCommand(), CancellationToken.None);

        var embeddedText = embeddings.AllTexts.Should().ContainSingle().Subject;
        embeddedText.Should().StartWith("Notice exclusive");
        embeddedText.Should().NotContain(privateMarker);
    }

    private static RecomputeBookNeighborsCommandHandler CreateHandler(
        RecommendationProfileTestDbContext context,
        CapturingEmbeddingService embeddings,
        CapturingNeighborWriter writer,
        bool enabled)
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(enabled);
        settings.NeighborsPerBook.Returns(30);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new RecomputeBookNeighborsCommandHandler(context, settings, embeddings, writer, clock);
    }

    private static SimilarityEdition Edition(
        string isbn13,
        string title,
        string? series = null,
        int? tome = null,
        string author = "Auteur",
        string? workId = null,
        string? summary = null) => new(
        isbn13,
        title,
        null,
        null,
        null,
        [author],
        [author],
        null,
        null,
        series,
        tome,
        workId,
        null,
        [],
        [],
        null,
        false,
        null,
        summary,
        null);

    private static string Summary(string opening) =>
        opening + " Il traverse plusieurs villes et rencontre des personnages qui changent le cours de son voyage. " +
        "Le récit conserve une voix claire et décrit ses enjeux sans interrompre son déroulement.";

    private static float[] Vector(float first, float second)
    {
        var vector = new float[BookSimilarityProfile.Dimensions];
        vector[0] = first;
        vector[1] = second;
        return vector;
    }
}

internal sealed class CapturingEmbeddingService(
    Func<string, float[]> vectorFactory,
    bool isConfigured = true) : IBookEmbeddingService
{
    public bool IsConfigured => isConfigured;
    public List<string[]> Calls { get; } = [];
    public IEnumerable<string> AllTexts => Calls.SelectMany(call => call);

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(texts.ToArray());
        IReadOnlyList<float[]> vectors = texts.Select(vectorFactory).ToArray();
        return Task.FromResult(vectors);
    }
}

internal sealed class CapturingNeighborWriter : IBookNeighborWriter
{
    public int GenerationsWritten { get; private set; }
    public IReadOnlyCollection<BookNeighborRow> Rows { get; private set; } = [];
    public Guid? GenerationId { get; private set; }
    public int BookCount { get; private set; }

    public Task WriteGenerationAsync(
        Guid generationId,
        IReadOnlyCollection<BookNeighborRow> rows,
        int bookCount,
        DateTime computedAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GenerationsWritten++;
        GenerationId = generationId;
        BookCount = bookCount;
        Rows = rows.ToArray();
        return Task.CompletedTask;
    }
}
