using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Application.Products.Queries.GetPublicProducts;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Products.Queries;

public class GetPublicProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyProductsVisibleOnWebsite()
    {
        var publicProduct = CreateProduct("Public", visibleOnWebsite: true);
        var cashOnlyProduct = CreateProduct("Caisse uniquement", visibleOnWebsite: false);
        var unavailableProduct = CreateProduct("Indisponible", available: false, visibleOnWebsite: true);
        var productRepository = Substitute.For<IProductRepository>();
        var products = new[] { publicProduct, cashOnlyProduct, unavailableProduct };

        productRepository.GetAllAsync().Returns(Task.FromResult<IEnumerable<Product>>(products));

        var handler = new GetPublicProductsQueryHandler(productRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new GetPublicProductsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var mappedProduct = result.Value.Should().ContainSingle().Which;
        mappedProduct.Name.Should().Be("Public");
        mappedProduct.VisibleOnWebsite.Should().BeTrue();
    }

    private static Product CreateProduct(string name, bool available = true, bool visibleOnWebsite = true)
    {
        return Product.Create(
            name,
            1,
            new Uri($"https://cdn.example.test/{name.Replace(' ', '-').ToLowerInvariant()}.png"),
            new ProductSection(ProductSection.ProductSectionEnum.Bingo),
            available,
            Array.Empty<Product>(),
            visibleOnWebsite: visibleOnWebsite);
    }
}
