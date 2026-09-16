using FluentAssertions;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBooks;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBookBySlug;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBooks;
using Vole_Papillon_Damour.Application.RareBooks.Queries.SearchRareBooksForCash;

namespace Vole_Papillon_Damour.Application.tests.RareBooks;

public sealed class RareBookQueryHandlerTests
{
    [Fact]
    public async Task Public_list_contains_published_sold_books_by_default_and_orders_by_price()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        await fixture.AddRareBookAsync("Petit prix", price: 10m, published: true);
        var sold = await fixture.AddRareBookAsync("Prix fort", price: 80m, published: true);
        sold.MarkSold(fixture.Now.AddMinutes(2), null, null, fixture.UserId);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddRareBookAsync("Brouillon", price: 100m);

        var result = await new GetPublicRareBooksQueryHandler(fixture.Context, fixture.Clock)
            .Handle(
                new GetPublicRareBooksQuery(
                    IncludeSold: true,
                    Sort: RareBookSortOrder.PriceDescending),
                CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Select(book => book.Title)
            .Should().ContainInOrder("Prix fort", "Petit prix");
        result.Value.Books.Should().NotContain(book => book.Title == "Brouillon");
        result.Value.Books.Single(book => book.Title == "Prix fort").IsSold.Should().BeTrue();
    }

    [Fact]
    public async Task Public_list_can_hide_sold_books_and_public_slug_hides_drafts()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var available = await fixture.AddRareBookAsync("Disponible", price: 10m, published: true);
        var sold = await fixture.AddRareBookAsync("Déjà vendu", price: 20m, published: true);
        sold.MarkSold(fixture.Now.AddMinutes(2), null, null, fixture.UserId);
        await fixture.Context.SaveChangesAsync();
        var draft = await fixture.AddRareBookAsync("Brouillon");

        var listResult = await new GetPublicRareBooksQueryHandler(fixture.Context, fixture.Clock)
            .Handle(new GetPublicRareBooksQuery(IncludeSold: false), CancellationToken.None);
        var detailResult = await new GetPublicRareBookBySlugQueryHandler(fixture.Context)
            .Handle(new GetPublicRareBookBySlugQuery(draft.Slug.Value), CancellationToken.None);
        var availableDetail = await new GetPublicRareBookBySlugQueryHandler(fixture.Context)
            .Handle(new GetPublicRareBookBySlugQuery(available.Slug.Value), CancellationToken.None);

        listResult.IsError.Should().BeFalse();
        listResult.Value.Books.Should().ContainSingle(book => book.Title == "Disponible");
        detailResult.IsError.Should().BeTrue();
        detailResult.FirstError.Code.Should().Be("RareBook.NotFound");
        availableDetail.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_list_supports_availability_price_and_missing_photo_filters()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var available = await fixture.AddRareBookAsync("Disponible", price: 30m, published: true);
        var sold = await fixture.AddRareBookAsync("Vendu", price: 70m, published: true);
        sold.MarkSold(fixture.Now.AddMinutes(2), null, null, fixture.UserId);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddPhotoAsync(available);
        await fixture.AddRareBookAsync("Brouillon", price: 10m);

        var result = await new GetAdminRareBooksQueryHandler(fixture.Context, fixture.Clock)
            .Handle(
                new GetAdminRareBooksQuery(
                    Availability: RareBookAdminAvailability.Available,
                    MinPrice: 20m,
                    MaxPrice: 40m,
                    WithoutPhoto: true),
                CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().BeEmpty();

        var availableResult = await new GetAdminRareBooksQueryHandler(fixture.Context, fixture.Clock)
            .Handle(
                new GetAdminRareBooksQuery(
                    Availability: RareBookAdminAvailability.Available,
                    MinPrice: 20m,
                    MaxPrice: 40m),
                CancellationToken.None);

        availableResult.IsError.Should().BeFalse();
        availableResult.Value.Books.Should().ContainSingle(book => book.Title == "Disponible");
    }

    [Fact]
    public async Task Admin_detail_and_cash_search_return_their_distinct_visibility_sets()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var published = await fixture.AddRareBookAsync("Recherche caisse", published: true);
        var sold = await fixture.AddRareBookAsync("Recherche vendue", published: true);
        sold.MarkSold(fixture.Now.AddMinutes(2), null, null, fixture.UserId);
        await fixture.Context.SaveChangesAsync();
        var draft = await fixture.AddRareBookAsync("Recherche brouillon");

        var detail = await new GetAdminRareBookQueryHandler(fixture.Context)
            .Handle(new GetAdminRareBookQuery(draft.Id), CancellationToken.None);
        var cash = await new SearchRareBooksForCashQueryHandler(fixture.Context, fixture.Clock)
            .Handle(new SearchRareBooksForCashQuery("caisse"), CancellationToken.None);

        detail.IsError.Should().BeFalse();
        detail.Value.Title.Should().Be("Recherche brouillon");
        cash.IsError.Should().BeFalse();
        cash.Value.Books.Should().ContainSingle(book => book.Id == published.Id.Value);
        cash.Value.Books.Should().NotContain(book => book.Title == "Recherche vendue");
        cash.Value.Books.Should().NotContain(book => book.Title == "Recherche brouillon");
    }
}
