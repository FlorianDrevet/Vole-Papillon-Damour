using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.RareBookAggregateTests;

// RG-173 / RG-174 : l'exemplaire rare garde son code interne à vie, séparé de son ISBN éventuel.
public sealed class RareBookInternalCodeTests
{
    private static readonly DateTime CreatedAt = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly BookCode InternalCode = BookCode.CreateInternalFromSequence(15);

    [Fact]
    public void Created_without_isbn_keeps_its_internal_code_as_label()
    {
        var rare = CreateWithoutIsbn();

        rare.Isbn13.Should().BeNull();
        rare.InternalCode.Should().Be(InternalCode);
        rare.LabelCode.Should().Be(InternalCode);
    }

    [Fact]
    public void Adding_an_isbn_later_keeps_the_internal_code_printed_on_the_label()
    {
        var rare = CreateWithoutIsbn();

        rare.Update("Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt.AddDays(1), Volunteer, IsbnCode());

        rare.Isbn13.Should().Be(IsbnCode());
        rare.InternalCode.Should().Be(InternalCode);
        rare.LabelCode.Should().Be(InternalCode);
    }

    [Fact]
    public void Created_with_isbn_has_no_internal_code_and_labels_with_the_isbn()
    {
        var rare = RareBook.CreateWithDetails(
            "Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt, Volunteer, IsbnCode());

        rare.InternalCode.Should().BeNull();
        rare.LabelCode.Should().Be(IsbnCode());
    }

    [Fact]
    public void Removing_the_isbn_is_refused_when_no_internal_code_exists()
    {
        var rare = RareBook.CreateWithDetails(
            "Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt, Volunteer, IsbnCode());

        var act = () => rare.Update("Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt.AddDays(1), Volunteer, null);

        act.Should().Throw<ArgumentException>();
        rare.Isbn13.Should().Be(IsbnCode());
    }

    [Fact]
    public void Removing_the_isbn_keeps_the_internal_code_when_one_exists()
    {
        var rare = CreateWithoutIsbn();
        rare.Update("Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt.AddDays(1), Volunteer, IsbnCode());

        rare.Update("Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt.AddDays(2), Volunteer, null);

        rare.Isbn13.Should().BeNull();
        rare.LabelCode.Should().Be(InternalCode);
    }

    [Fact]
    public void The_isbn_field_refuses_an_internal_code()
    {
        var act = () => RareBook.CreateWithDetails(
            "Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt, Volunteer, InternalCode);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_internal_code_field_refuses_an_isbn()
    {
        var act = () => RareBook.CreateWithDetails(
            "Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt, Volunteer,
            internalCode: IsbnCode());

        act.Should().Throw<ArgumentException>();
    }

    private static RareBook CreateWithoutIsbn() =>
        RareBook.CreateWithDetails(
            "Titre", null, null, null, 10m, RareBookCondition.AsNew, null, CreatedAt, Volunteer,
            internalCode: InternalCode);

    private static BookCode IsbnCode()
    {
        Isbn13.TryCreate("9780306406157", out var isbn13).Should().BeTrue();
        return isbn13;
    }
}
