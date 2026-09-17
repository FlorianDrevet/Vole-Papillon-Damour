using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;
using Vole_Papillon_Damour.Domain.BookAggregate;

namespace Vole_Papillon_Damour.Application.tests.Books;

public sealed class LegacyRareFlagRemovalTests
{
    [Fact]
    public void Book_no_longer_exposes_the_legacy_rare_flag_or_mutator()
    {
        typeof(Book).GetProperty("IsRare").Should().BeNull();
        typeof(Book).GetMethod("UpdateRareStatus").Should().BeNull();
    }

    [Fact]
    public void Legacy_rare_command_and_result_are_removed_from_the_application_surface()
    {
        var applicationAssembly = typeof(SearchCatalogQuery).Assembly;

        applicationAssembly.GetTypes()
            .Select(type => type.Name)
            .Should()
            .NotContain("MarkBookRareCommandHandler");
        applicationAssembly.GetTypes()
            .Where(type => type.Name.Contains("BookFlag", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .Should()
            .NotContain("IsRare");
    }
}
