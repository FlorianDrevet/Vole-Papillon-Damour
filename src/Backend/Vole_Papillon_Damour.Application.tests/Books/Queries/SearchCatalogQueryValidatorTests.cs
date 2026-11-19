using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

public sealed class SearchCatalogQueryValidatorTests
{
    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_CapsSearchTextLength(int length, bool expectedValid)
    {
        var validator = new SearchCatalogQueryValidator();

        var result = validator.Validate(CreateQuery(new string('a', length)));

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void Validate_WhenNoSearchTextIsProvided_IsValid()
    {
        var validator = new SearchCatalogQueryValidator();

        var result = validator.Validate(CreateQuery(null));

        result.IsValid.Should().BeTrue();
    }

    private static SearchCatalogQuery CreateQuery(string? search) =>
        new(search, null, PublicCatalogAvailabilityFilter.All, RareOnly: false,
            PublicCatalogSortOrder.Relevance, Page: 1, PageSize: 24);
}
