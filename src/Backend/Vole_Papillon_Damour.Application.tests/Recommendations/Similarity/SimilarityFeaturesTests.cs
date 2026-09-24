using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class SimilarityFeaturesTests
{
    internal static SimilarityEdition Edition(
        string isbn,
        string title,
        string[]? surnames = null,
        string? bnfWorkId = null,
        string? olWorkKey = null,
        string? seriesTitle = null,
        int? seriesNumber = null,
        int? partNumber = null,
        string? collection = null,
        string? publisher = null,
        string? audience333 = null,
        bool cnlj = false,
        string[]? forms = null,
        string[]? subjects = null,
        string? dewey = null,
        string? summary = null,
        string? olDescription = null) =>
        new(isbn, title, null, partNumber, null,
            (surnames ?? ["Auteur"]).Select(s => $"Prénom {s}").ToArray(), surnames ?? ["Auteur"],
            publisher, collection, seriesTitle, seriesNumber, bnfWorkId, olWorkKey,
            forms ?? [], subjects ?? [], audience333, cnlj, dewey, summary, olDescription);

    [Fact]
    public void SameWork_WithSharedBnfWorkId_IsTrue()
    {
        var a = SimilarityFeatureBuilder.Build(Edition("9782070612758", "Le petit prince", bnfWorkId: "11958514"));
        var b = SimilarityFeatureBuilder.Build(Edition("9782075224055", "Le Petit Prince", bnfWorkId: "11958514"));

        SimilarityFeatureBuilder.SameWork(a, b).Should().BeTrue();
    }

    [Fact]
    public void SameWork_WithDifferentTomes_IsFalse()
    {
        var tome1 = SimilarityFeatureBuilder.Build(Edition("9782266149518", "Retour à l'état sauvage", bnfWorkId: "SERIE", seriesTitle: "La guerre des clans", seriesNumber: 1));
        var tome2 = SimilarityFeatureBuilder.Build(Edition("9782266156523", "À feu et à sang", bnfWorkId: "SERIE", seriesTitle: "La guerre des clans", seriesNumber: 2));

        SimilarityFeatureBuilder.SameWork(tome1, tome2).Should().BeFalse();
    }

    [Fact]
    public void SameWork_WithSameTitleAndOneSharedAuthorAmongTranslators_IsTrue()
    {
        var a = SimilarityFeatureBuilder.Build(Edition("9782070584628", "Harry Potter à l'école des sorciers", surnames: ["Rowling", "Ménard"]));
        var b = SimilarityFeatureBuilder.Build(Edition("9782070518425", "Harry Potter à l'école des sorciers", surnames: ["Rowling"]));

        SimilarityFeatureBuilder.SameWork(a, b).Should().BeTrue();
    }

    [Fact]
    public void SameWork_WithSameTitleButDifferentAuthors_IsFalse()
    {
        var novel = SimilarityFeatureBuilder.Build(Edition("9782070368228", "1984", surnames: ["Orwell"]));
        var comic = SimilarityFeatureBuilder.Build(Edition("9782380120318", "1984", surnames: ["Coste"]));

        SimilarityFeatureBuilder.SameWork(novel, comic).Should().BeFalse();
    }

    [Theory]
    [InlineData("À partir de 11 ans", false, null, null, SimilarityAudience.Youth)]
    [InlineData("À partir de 13 ans", false, null, null, SimilarityAudience.Teen)]
    [InlineData(null, true, null, null, SimilarityAudience.Youth)]
    [InlineData(null, false, "Folio junior", null, SimilarityAudience.Youth)]
    [InlineData(null, false, "Wiz", "Albin Michel", SimilarityAudience.Youth)]
    [InlineData(null, false, "Folio", "Gallimard", SimilarityAudience.Adult)]
    public void InferAudience(string? audience333, bool cnlj, string? collection, string? publisher, SimilarityAudience expected) =>
        SimilarityFeatureBuilder.InferAudience(Edition("9782070612758", "Titre", audience333: audience333, cnlj: cnlj, collection: collection, publisher: publisher))
            .Should().Be(expected);

    [Theory]
    [InlineData(null, "Kana", null, SimilarityForm.Illustrated)]
    [InlineData("Bandes dessinées", null, null, SimilarityForm.Illustrated)]
    [InlineData(null, "Dargaud", null, SimilarityForm.Illustrated)]
    [InlineData(null, "Albin Michel", "909", SimilarityForm.Essay)]
    [InlineData("Romans", "Gallimard", null, SimilarityForm.Text)]
    public void InferForm(string? form, string? publisher, string? dewey, SimilarityForm expected) =>
        SimilarityFeatureBuilder.InferForm(Edition("9782070612758", "Titre", forms: form is null ? [] : [form], publisher: publisher, dewey: dewey))
            .Should().Be(expected);

    [Fact]
    public void ComposeText_SkipsEmptySegmentsAndEndsWithWorkSummary()
    {
        var edition = Edition("9782742793099", "Les hommes qui n'aimaient pas les femmes", surnames: ["Larsson"], seriesTitle: "Millénium", seriesNumber: 1, collection: "Babel noir", forms: ["Romans"]);

        var text = SimilarityFeatureBuilder.ComposeText(edition, "Mikael Blomkvist enquête sur une disparition vieille de quarante ans.");

        text.Should().Be("Les hommes qui n'aimaient pas les femmes. Auteur : Prénom Larsson. Collection : Babel noir. Série : Millénium, tome 1. Genre : Romans. Mikael Blomkvist enquête sur une disparition vieille de quarante ans.");
    }
}
