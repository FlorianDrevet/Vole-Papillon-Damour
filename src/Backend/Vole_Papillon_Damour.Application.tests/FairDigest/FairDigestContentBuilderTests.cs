using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.FairDigest;

public sealed class FairDigestContentBuilderTests
{
    [Fact]
    public void Build_ExcludesAWorkAlreadyIncludedInNewsAndKeepsNewsPriority()
    {
        var newBook = Book("9782070363735", "OL1W");
        var duplicateEdition = Book("9783140464079", "OL1W");
        var returnedBook = Book("9780306406157", "OL2W");
        var builder = CreateBuilder();

        var result = builder.Build(
            [new PersonalRecommendation(newBook, NeighborReason.Theme, "Dune", 4)],
            [
                new FairDigestBackItem(duplicateEdition, 9, true),
                new FairDigestBackItem(returnedBook, 5, true),
            ]);

        result.News.Should().ContainSingle().Which.Book.Should().Be(newBook);
        result.Back.Should().ContainSingle().Which.Book.Should().Be(returnedBook);
        result.SkippedNoContent.Should().BeFalse();
    }

    [Fact]
    public void Build_SkipsWhenThereAreNoNewsAndFewerThanThreeReturns()
    {
        var result = CreateBuilder().Build(
            [],
            [
                new FairDigestBackItem(Book("9782070363735", "OL1W"), 2, true),
                new FairDigestBackItem(Book("9783140464079", "OL2W"), 1, true),
            ]);

        result.News.Should().BeEmpty();
        result.Back.Should().HaveCount(2);
        result.SkippedNoContent.Should().BeTrue();
    }

    private static FairDigestContentBuilder CreateBuilder() =>
        new(Options.Create(new FairDigestOptions
        {
            NewsMax = 8,
            BackMax = 6,
            BackMinCount = 3,
        }));

    private static PublicCatalogBookResult Book(string isbn, string workId) =>
        new(
            isbn,
            "Livre",
            "Auteur",
            "Éditeur",
            2020,
            "Paperback",
            "fr",
            "Roman",
            workId,
            null,
            1,
            0,
            null,
            null,
            DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
            false);
}
