using FluentAssertions;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.RareBookAggregateTests;

public sealed class ValueObjectsTests
{
    [Fact]
    public void RareBookCondition_ExposesTheFourMaquetteValues()
    {
        var values = Enum.GetValues<RareBookCondition.RareBookConditionEnum>();

        values.Should().Equal(
            RareBookCondition.RareBookConditionEnum.AsNew,
            RareBookCondition.RareBookConditionEnum.GoodWithFlaws,
            RareBookCondition.RareBookConditionEnum.Worn,
            RareBookCondition.RareBookConditionEnum.Damaged);
    }

    [Fact]
    public void RareBookShelf_AcceptsConfiguredAndCustomLabels()
    {
        var configured = RareBookShelf.Create("Éditions anciennes");
        var custom = RareBookShelf.Create("Sciences naturelles");

        configured.Value.Should().Be("Éditions anciennes");
        custom.Value.Should().Be("Sciences naturelles");
    }

    [Fact]
    public void RareBookShelf_WithEmptyLabel_Throws()
    {
        var action = () => RareBookShelf.Create(" ");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RareBookSlug_NormalizesAccentsAndSupportsCollisionSuffix()
    {
        var slug = RareBookSlug.Create("Les Fables de la Fontaine", "Gustave Doré", 1868);

        slug.Value.Should().Be("les-fables-de-la-fontaine-gustave-dore-1868");
        slug.WithCollisionSuffix(2).Value
            .Should().Be("les-fables-de-la-fontaine-gustave-dore-1868-2");
    }

    [Fact]
    public void RareBookSlug_TruncatesToOneHundredTwentyCharacters()
    {
        var slug = RareBookSlug.Create(new string('é', 140), null, null);

        slug.Value.Length.Should().Be(120);
        slug.Value.Should().NotEndWith("-");
    }
}
