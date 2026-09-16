using FluentAssertions;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Commands.AddRareBookPhoto;
using Vole_Papillon_Damour.Application.RareBooks.Commands.CreateRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBookPhoto;
using Vole_Papillon_Damour.Application.RareBooks.Commands.MarkRareBookSold;
using Vole_Papillon_Damour.Application.RareBooks.Commands.PublishRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.ReorderRareBookPhotos;
using Vole_Papillon_Damour.Application.RareBooks.Commands.RestoreRareBookAvailability;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UnpublishRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBookPhotoCaption;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBooks;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBookBySlug;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBooks;
using Vole_Papillon_Damour.Application.RareBooks.Queries.SearchRareBooksForCash;

namespace Vole_Papillon_Damour.Application.tests.RareBooks;

public sealed class RareBookApplicationContractTests
{
    [Fact]
    public void Every_lot_3_intention_has_a_mediatR_request_and_result()
    {
        var commandTypes = new[]
        {
            typeof(CreateRareBookCommand),
            typeof(UpdateRareBookCommand),
            typeof(PublishRareBookCommand),
            typeof(UnpublishRareBookCommand),
            typeof(DeleteRareBookCommand),
            typeof(MarkRareBookSoldCommand),
            typeof(RestoreRareBookAvailabilityCommand),
            typeof(AddRareBookPhotoCommand),
            typeof(ReorderRareBookPhotosCommand),
            typeof(UpdateRareBookPhotoCaptionCommand),
            typeof(DeleteRareBookPhotoCommand)
        };

        commandTypes.Should().OnlyContain(type =>
            type.GetInterfaces().Any(@interface =>
                @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == typeof(IRequest<>)));

        var queryTypes = new[]
        {
            typeof(GetPublicRareBooksQuery),
            typeof(GetPublicRareBookBySlugQuery),
            typeof(GetAdminRareBooksQuery),
            typeof(GetAdminRareBookQuery),
            typeof(SearchRareBooksForCashQuery)
        };

        queryTypes.Should().OnlyContain(type =>
            type.GetInterfaces().Any(@interface =>
                @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == typeof(IRequest<>)));
    }

    [Fact]
    public void Rare_book_results_keep_the_price_as_a_display_value_without_a_total()
    {
        typeof(RareBookResult).GetProperty(nameof(RareBookResult.Price)).Should().NotBeNull();
        typeof(RareBookResult).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("Total", "the application never counts money");
    }
}
