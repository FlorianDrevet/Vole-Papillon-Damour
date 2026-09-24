using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Contracts.Books.Responses;
using Vole_Papillon_Damour.Contracts.RareBooks.Requests;
using Vole_Papillon_Damour.Contracts.RareBooks.Responses;

namespace Vole_Papillon_Damour.Api.tests;

public sealed class RareBookContractTests
{
    [Fact]
    public void Rare_book_requests_expose_typed_fields_for_forms_and_commands()
    {
        typeof(CreateRareBookRequest).GetProperty(nameof(CreateRareBookRequest.Title)).Should().NotBeNull();
        typeof(UpdateRareBookRequest).GetProperty(nameof(UpdateRareBookRequest.RowVersion)).Should().NotBeNull();
        typeof(MarkRareBookSoldRequest).GetProperty(nameof(MarkRareBookSoldRequest.OccurredAt)).Should().NotBeNull();
        typeof(AddRareBookPhotoRequest).GetProperty(nameof(AddRareBookPhotoRequest.File))
            ?.PropertyType.Should().Be(typeof(IFormFile));
    }

    [Fact]
    public void Rare_book_responses_include_price_but_never_a_total()
    {
        typeof(RareBookResponse).GetProperty(nameof(RareBookResponse.Price)).Should().NotBeNull();
        typeof(PublicRareBookResponse).GetProperty(nameof(PublicRareBookResponse.Price)).Should().NotBeNull();
        typeof(RareBookResponse).GetProperties().Select(property => property.Name)
            .Should().NotContain("Total");
    }

    [Fact]
    public void Rare_book_contracts_do_not_expose_removed_details_or_shelf_classification()
    {
        var removedProperties = new[]
        {
            "Binding",
            "Dimensions",
            "PageCount",
            "ShelfLocation",
            "PriceSetBy",
            "Shelf"
        };
        var contracts = new[]
        {
            typeof(CreateRareBookRequest),
            typeof(UpdateRareBookRequest),
            typeof(RareBookResponse),
            typeof(PublicRareBookResponse),
            typeof(CashRareBookResponse),
            typeof(ScanCatalogRareBookResponse)
        };

        foreach (var contract in contracts)
        {
            contract.GetProperties().Select(property => property.Name)
                .Should().NotContain(removedProperties);
        }

        typeof(PublicRareBookPageResponse).GetProperty("Shelves").Should().BeNull();
        typeof(PublicRareBookPageResponse).Assembly.GetTypes()
            .Should().NotContain(type => type.Name == "RareBookShelfCountResponse");
    }
}
