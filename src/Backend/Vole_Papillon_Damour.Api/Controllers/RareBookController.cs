using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.RareBooks.Commands.AddRareBookPhoto;
using Vole_Papillon_Damour.Application.RareBooks.Commands.CreateRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBookPhoto;
using Vole_Papillon_Damour.Application.RareBooks.Commands.MarkRareBookSold;
using Vole_Papillon_Damour.Application.RareBooks.Commands.PublishRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.ReorderRareBookPhotos;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UnpublishRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBookPhotoCaption;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBooks;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBookBySlug;
using Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBooks;
using Vole_Papillon_Damour.Application.RareBooks.Queries.SearchRareBooksForCash;
using Vole_Papillon_Damour.Contracts.RareBooks.Requests;
using Vole_Papillon_Damour.Contracts.RareBooks.Responses;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class RareBookController
{
    public static IApplicationBuilder UseRareBookController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/catalog/rare-books",
                    async (
                        string? shelf,
                        bool? includeSold,
                        string? sort,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryParseSort(sort, out var sortOrder))
                        {
                            return DomainErrors.RareBook.InvalidSort().Result();
                        }

                        var result = await mediator.Send(
                            new GetPublicRareBooksQuery(
                                shelf,
                                includeSold ?? true,
                                sortOrder,
                                page ?? 1,
                                pageSize ?? 24),
                            cancellationToken);

                        return result.Match(
                            books => Results.Ok(ToResponse(books)),
                            error => error.Result());
                    })
                .WithName("GetPublicRareBooks")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/catalog/rare-books/{slug}",
                    async (
                        string slug,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetPublicRareBookBySlugQuery(slug),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("GetPublicRareBookBySlug")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/rare-books/admin",
                    async (
                        string? search,
                        string? status,
                        string? availability,
                        bool? hasIsbn,
                        decimal? minPrice,
                        decimal? maxPrice,
                        bool? withoutPhoto,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryParseAvailability(availability, out var availabilityFilter))
                        {
                            return DomainErrors.RareBook.InvalidData(
                                "availability must be one of all, available, or sold.").Result();
                        }

                        var result = await mediator.Send(
                            new GetAdminRareBooksQuery(
                                search,
                                status,
                                availabilityFilter,
                                hasIsbn,
                                minPrice,
                                maxPrice,
                                withoutPhoto == true,
                                page ?? 1,
                                pageSize ?? 50),
                            cancellationToken);

                        return result.Match(
                            books => Results.Ok(ToResponse(books)),
                            error => error.Result());
                    })
                .WithName("GetAdminRareBooks")
                .RequireAuthorization("RareBooks");

            endpoints.MapGet(
                    "/rare-books/admin/{id:guid}",
                    async (
                        Guid id,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        var result = await mediator.Send(
                            new GetAdminRareBookQuery(rareBookId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("GetAdminRareBook")
                .RequireAuthorization("RareBooks");

            endpoints.MapGet(
                    "/rare-books/cash/search",
                    async (
                        string? search,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new SearchRareBooksForCashQuery(
                                search,
                                page ?? 1,
                                pageSize ?? 25),
                            cancellationToken);

                        return result.Match(
                            books => Results.Ok(ToResponse(books)),
                            error => error.Result());
                    })
                .WithName("SearchRareBooksForCash")
                .RequireAuthorization("ScanVolunteer");

            endpoints.MapPost(
                    "/rare-books/admin",
                    async (
                        CreateRareBookRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new CreateRareBookCommand(
                                request.Title,
                                request.AuthorMention,
                                request.Publisher,
                                request.PublicationYear,
                                request.Shelf,
                                request.Price,
                                request.Condition,
                                request.PublicDescription,
                                request.Binding,
                                request.Dimensions,
                                request.PageCount,
                                request.ShelfLocation,
                                request.PriceSetBy,
                                request.Isbn13,
                                userId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Created($"/rare-books/admin/{book.Id}", ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("CreateRareBook")
                .RequireAuthorization("RareBooks");

            endpoints.MapPut(
                    "/rare-books/admin/{id:guid}",
                    async (
                        Guid id,
                        UpdateRareBookRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
                        {
                            return DomainErrors.RareBook.InvalidData("rowVersion must be a base64 value.").Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new UpdateRareBookCommand(
                                rareBookId,
                                request.Title,
                                request.AuthorMention,
                                request.Publisher,
                                request.PublicationYear,
                                request.Shelf,
                                request.Price,
                                request.Condition,
                                request.PublicDescription,
                                request.Binding,
                                request.Dimensions,
                                request.PageCount,
                                request.ShelfLocation,
                                request.PriceSetBy,
                                request.Isbn13,
                                rowVersion,
                                userId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("UpdateRareBook")
                .RequireAuthorization("RareBooks");

            endpoints.MapPost(
                    "/rare-books/admin/{id:guid}/publish",
                    async (
                        Guid id,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new PublishRareBookCommand(rareBookId, userId),
                            cancellationToken);

                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("PublishRareBook")
                .RequireAuthorization("RareBooks");

            endpoints.MapPost(
                    "/rare-books/admin/{id:guid}/unpublish",
                    async (
                        Guid id,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new UnpublishRareBookCommand(rareBookId, userId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("UnpublishRareBook")
                .RequireAuthorization("RareBooks");

            endpoints.MapPost(
                    "/rare-books/admin/{id:guid}/sold",
                    async (
                        Guid id,
                        MarkRareBookSoldRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        if (!TryGetOptionalScanSessionId(request.ScanSessionId, out var scanSessionId) ||
                            !TryGetOptionalAssoEventsId(request.AssoEventsId, out var assoEventsId))
                        {
                            return DomainErrors.RareBook.InvalidData("Related identifiers must be non-empty GUIDs.").Result();
                        }

                        var result = await mediator.Send(
                            new MarkRareBookSoldCommand(
                                rareBookId,
                                scanSessionId,
                                assoEventsId,
                                request.OccurredAt,
                                userId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("MarkRareBookSold")
                .RequireAuthorization("RareBooks");

            endpoints.MapDelete(
                    "/rare-books/admin/{id:guid}",
                    async (
                        Guid id,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new DeleteRareBookCommand(rareBookId, userId),
                            cancellationToken);

                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("DeleteRareBook")
                .RequireAuthorization("RareBooks");

            endpoints.MapPost(
                    "/rare-books/admin/{id:guid}/photos",
                    async (
                        Guid id,
                        [FromForm] AddRareBookPhotoRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        if (request.File is null)
                        {
                            return DomainErrors.RareBook.InvalidData("A photo file is required.").Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        await using var stream = request.File.OpenReadStream();
                        var result = await mediator.Send(
                            new AddRareBookPhotoCommand(
                                rareBookId,
                                stream,
                                request.File.FileName,
                                request.File.ContentType,
                                request.File.Length,
                                request.Caption,
                                userId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("AddRareBookPhoto")
                .DisableAntiforgery()
                .RequireAuthorization("RareBooks");

            endpoints.MapPut(
                    "/rare-books/admin/{id:guid}/photos/order",
                    async (
                        Guid id,
                        ReorderRareBookPhotosRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookId(id, out var rareBookId))
                        {
                            return DomainErrors.RareBook.InvalidId().Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        if (request.PhotoIds is null ||
                            request.PhotoIds.Any(photoId => photoId == Guid.Empty))
                        {
                            return DomainErrors.RareBook.InvalidPhotoOrder().Result();
                        }

                        var result = await mediator.Send(
                            new ReorderRareBookPhotosCommand(
                                rareBookId,
                                request.PhotoIds.Select(RareBookPhotoId.Create).ToArray(),
                                userId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("ReorderRareBookPhotos")
                .RequireAuthorization("RareBooks");

            endpoints.MapPatch(
                    "/rare-books/admin/photos/{photoId:guid}",
                    async (
                        Guid photoId,
                        UpdateRareBookPhotoCaptionRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookPhotoId(photoId, out var rareBookPhotoId))
                        {
                            return DomainErrors.RareBook.InvalidPhotoId().Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new UpdateRareBookPhotoCaptionCommand(
                                rareBookPhotoId,
                                request.Caption,
                                userId),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("UpdateRareBookPhotoCaption")
                .RequireAuthorization("RareBooks");

            endpoints.MapDelete(
                    "/rare-books/admin/photos/{photoId:guid}",
                    async (
                        Guid photoId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetRareBookPhotoId(photoId, out var rareBookPhotoId))
                        {
                            return DomainErrors.RareBook.InvalidPhotoId().Result();
                        }

                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new DeleteRareBookPhotoCommand(rareBookPhotoId, userId),
                            cancellationToken);

                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("DeleteRareBookPhoto")
                .RequireAuthorization("RareBooks");
        });
    }

    private static bool TryParseSort(string? value, out RareBookSortOrder sortOrder)
    {
        sortOrder = RareBookSortOrder.Recent;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        sortOrder = value.Trim().ToLowerInvariant() switch
        {
            "price-desc" => RareBookSortOrder.PriceDescending,
            "price-asc" => RareBookSortOrder.PriceAscending,
            "recent" => RareBookSortOrder.Recent,
            _ => sortOrder
        };

        return value.Trim().ToLowerInvariant() is "price-desc" or "price-asc" or "recent";
    }

    private static bool TryParseAvailability(
        string? value,
        out RareBookAdminAvailability availability)
    {
        availability = RareBookAdminAvailability.All;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        availability = value.Trim().ToLowerInvariant() switch
        {
            "all" => RareBookAdminAvailability.All,
            "available" => RareBookAdminAvailability.Available,
            "sold" => RareBookAdminAvailability.Sold,
            _ => availability
        };

        return value.Trim().ToLowerInvariant() is "all" or "available" or "sold";
    }

    private static bool TryGetRareBookId(Guid value, out RareBookId id)
    {
        if (value == Guid.Empty)
        {
            id = null!;
            return false;
        }

        id = RareBookId.Create(value);
        return true;
    }

    private static bool TryGetRareBookPhotoId(Guid value, out RareBookPhotoId id)
    {
        if (value == Guid.Empty)
        {
            id = null!;
            return false;
        }

        id = RareBookPhotoId.Create(value);
        return true;
    }

    private static bool TryGetOptionalScanSessionId(Guid? value, out ScanSessionId? id)
    {
        if (value is null)
        {
            id = null;
            return true;
        }

        if (value == Guid.Empty)
        {
            id = null;
            return false;
        }

        id = ScanSessionId.Create(value.Value);
        return true;
    }

    private static bool TryGetOptionalAssoEventsId(Guid? value, out AssoEventsId? id)
    {
        if (value is null)
        {
            id = null;
            return true;
        }

        if (value == Guid.Empty)
        {
            id = null;
            return false;
        }

        id = AssoEventsId.Create(value.Value);
        return true;
    }

    private static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out UserId userId)
    {
        var externalId = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(
                "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        if (Guid.TryParse(externalId, out var value) && value != Guid.Empty)
        {
            userId = UserId.Create(value);
            return true;
        }

        userId = null!;
        return false;
    }

    private static PublicRareBookPageResponse ToResponse(PublicRareBookPageResult result) =>
        new(
            result.GeneratedAt,
            result.Books.Select(ToResponse).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.Shelves);

    private static PublicRareBookDetailResponse ToResponse(PublicRareBookDetailResult result) =>
        new(
            ToResponse(result.RareBook),
            result.RelatedBooks.Select(ToResponse).ToArray());

    private static PublicRareBookResponse ToResponse(PublicRareBookResult result) =>
        new(
            result.Id,
            result.Slug,
            result.Isbn13,
            result.Title,
            result.AuthorMention,
            result.Publisher,
            result.PublicationYear,
            result.Shelf,
            result.Price,
            result.Condition,
            result.PublicDescription,
            result.Binding,
            result.Dimensions,
            result.PageCount,
            result.Status,
            result.IsSold,
            result.SoldAt,
            result.Photos.Select(ToResponse).ToArray());

    private static RareBookPageResponse ToResponse(RareBookPageResult result) =>
        new(
            result.GeneratedAt,
            result.Books.Select(ToResponse).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);

    private static RareBookResponse ToResponse(RareBookResult result) =>
        new(
            result.Id,
            result.Slug,
            result.Isbn13,
            result.Title,
            result.AuthorMention,
            result.Publisher,
            result.PublicationYear,
            result.Shelf,
            result.Price,
            result.Condition,
            result.PublicDescription,
            result.Binding,
            result.Dimensions,
            result.PageCount,
            result.ShelfLocation,
            result.Status,
            result.IsSold,
            result.SoldAt,
            result.SoldAtFairId,
            result.SoldInSessionId,
            result.PriceSetBy,
            result.CreatedAt,
            result.CreatedBy,
            result.UpdatedAt,
            result.UpdatedBy,
            Convert.ToBase64String(result.RowVersion),
            result.Photos.Select(ToResponse).ToArray());

    private static RareBookPublishResponse ToResponse(RareBookPublishResult result) =>
        new(ToResponse(result.RareBook), result.Changed, result.Warnings);

    private static RareBookPhotoResponse ToResponse(RareBookPhotoResult result) =>
        new(
            result.Id,
            result.BlobUri,
            result.BlobName,
            result.Caption,
            result.Position,
            result.ContentType,
            result.SizeBytes,
            result.UploadedAt,
            result.UploadedBy);

    private static RareBookCashSearchResponse ToResponse(RareBookCashSearchResult result) =>
        new(
            result.GeneratedAt,
            result.Books.Select(book => new CashRareBookResponse(
                book.Id,
                book.Title,
                book.AuthorMention,
                book.Isbn13,
                book.Price,
                book.Shelf,
                book.Condition,
                book.Thumbnail,
                book.Slug)).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
}
