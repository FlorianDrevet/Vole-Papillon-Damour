using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Common;

public sealed class PublicBookPathTests
{
    [Fact]
    public void For_BuildsTheCatalogSlugFromTitleAuthorsAndIsbn()
    {
        var path = PublicBookPath.For(
            ParseIsbn("9782070408504"),
            "Le Petit Prince",
            "Antoine de Saint-Exupéry");

        path.Should().Be("/livres/le-petit-prince-antoine-de-saint-exupery-9782070408504");
    }

    [Fact]
    public void For_WithoutTitleOrAuthorsFallsBackToLivre()
    {
        var path = PublicBookPath.For(ParseIsbn("9782070408504"), null, "  ");

        path.Should().Be("/livres/livre-9782070408504");
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
