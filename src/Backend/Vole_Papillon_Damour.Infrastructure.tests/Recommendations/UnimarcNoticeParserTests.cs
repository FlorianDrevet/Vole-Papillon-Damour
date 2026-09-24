using System.Xml.Linq;
using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

namespace Vole_Papillon_Damour.Infrastructure.tests.Recommendations;

public sealed class UnimarcNoticeParserTests
{
    [Fact]
    public void Parse_MapsRecommendationFieldsAndIgnoresTranslatorRole()
    {
        var record = XElement.Parse(RecordedNotice);

        var edition = UnimarcNoticeParser.Parse(record, "9782742793099");

        edition.Title.Should().Be("Les hommes qui n'aimaient pas les femmes");
        edition.PartTitle.Should().Be("Première partie");
        edition.PartNumber.Should().Be(1);
        edition.Subtitle.Should().Be("Le roman");
        edition.Authors.Should().Equal("Stieg Larsson", "Suzanne Collins");
        edition.AuthorSurnames.Should().Equal("Larsson", "Collins");
        edition.SeriesTitle.Should().Be("Millénium");
        edition.SeriesNumber.Should().Be(1);
        edition.BnfWorkId.Should().Be("40221640");
        edition.CnljReviewed.Should().BeTrue();
        edition.Forms.Should().Contain("Romans");
        edition.Subjects.Should().Contain(["Enquêtes", "Disparitions", "Femmes"]);
        edition.Audience333.Should().Be("À partir de 15 ans");
        edition.Languages.Should().Equal("fre");
        edition.EditionSummary.Should().Be("Mikael Blomkvist enquête sur une disparition vieille de quarante ans.");
    }

    [Fact]
    public void Parse_WithoutSummary_ReturnsNullSummary()
    {
        var record = XElement.Parse("""
            <record><datafield tag="200"><subfield code="a">Titre</subfield></datafield></record>
            """);

        UnimarcNoticeParser.Parse(record, "9782742793099").EditionSummary.Should().BeNull();
    }

    private const string RecordedNotice = """
        <record>
          <datafield tag="010"><subfield code="a">9782742793099</subfield></datafield>
          <datafield tag="200">
            <subfield code="a">Les hommes qui n'aimaient pas les femmes</subfield>
            <subfield code="e">Le roman</subfield>
            <subfield code="h">Première partie</subfield>
            <subfield code="i">1</subfield>
          </datafield>
          <datafield tag="700"><subfield code="a">Larsson</subfield><subfield code="b">Stieg</subfield></datafield>
          <datafield tag="702"><subfield code="a">Ménard</subfield><subfield code="b">Jean-François</subfield><subfield code="4">730</subfield></datafield>
          <datafield tag="702"><subfield code="a">Collins</subfield><subfield code="b">Suzanne</subfield></datafield>
          <datafield tag="210"><subfield code="c">Actes Sud</subfield></datafield>
          <datafield tag="225"><subfield code="a">Babel noir</subfield></datafield>
          <datafield tag="461"><subfield code="t">Millénium</subfield><subfield code="v">1</subfield></datafield>
          <datafield tag="500"><subfield code="3">40221640</subfield></datafield>
          <datafield tag="608"><subfield code="a">Romans</subfield><subfield code="2">CNLJ</subfield></datafield>
          <datafield tag="606"><subfield code="a">Enquêtes</subfield><subfield code="x">Disparitions</subfield><subfield code="a">Femmes</subfield></datafield>
          <datafield tag="333"><subfield code="a">À partir de 15 ans</subfield></datafield>
          <datafield tag="101"><subfield code="a">fre</subfield></datafield>
          <datafield tag="330"><subfield code="a">Mikael Blomkvist enquête sur une disparition vieille de quarante ans.</subfield></datafield>
        </record>
        """;
}
