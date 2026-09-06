using System.Security.Claims;
using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Books.Commands.RegisterSale;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.GetCatalogDelta;
using Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;
using Vole_Papillon_Damour.Application.Books.Queries.GetDeadStock;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicBook;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicCatalogSitemap;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicNextBookFair;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicWork;
using Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RemoveWatchlistItem;
using Vole_Papillon_Damour.Application.WatchlistFeature.Queries.GetMyWatchlist;
using Vole_Papillon_Damour.Contracts.Books.Requests;
using Vole_Papillon_Damour.Contracts.Books.Responses;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class BookController
{
    public static IApplicationBuilder UseBookController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/catalog/search",
                    async (
                        [FromQuery(Name = "q")] string? search,
                        string? genre,
                        string? availability,
                        bool? rare,
                        string? sort,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryParseAvailability(availability, out var availabilityFilter))
                        {
                            return Results.BadRequest(new
                            {
                                error = "availability must be one of all, available, or next-fair."
                            });
                        }

                        if (!TryParseSort(sort, out var sortOrder))
                        {
                            return Results.BadRequest(new
                            {
                                error = "sort must be one of relevance or recent."
                            });
                        }

                        var result = await mediator.Send(
                            new SearchCatalogQuery(
                                search,
                                genre,
                                availabilityFilter,
                                rare == true,
                                sortOrder,
                                page ?? 1,
                                pageSize ?? 24),
                            cancellationToken);

                        return result.Match(
                            catalog => Results.Ok(ToResponse(catalog)),
                            error => error.Result());
                    })
                .WithName("SearchPublicCatalog")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/catalog/books/{isbn13}",
                    async (
                        string isbn13,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetPublicBookQuery(isbn13),
                            cancellationToken);

                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("GetPublicCatalogBook")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/catalog/fairs/next",
                    async (
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetPublicNextBookFairQuery(),
                            cancellationToken);

                        return result.Match(
                            fair => Results.Ok(ToResponse(fair)),
                            error => error.Result());
                    })
                .WithName("GetNextPublicBookFair")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/catalog/works/{workId}",
                    async (
                        string workId,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetPublicWorkQuery(workId),
                            cancellationToken);

                        return result.Match(
                            work => Results.Ok(new PublicCatalogWorkResponse(
                                work.WorkId,
                                work.Title,
                                work.Authors,
                                work.Editions.Select(ToResponse).ToArray())),
                            error => error.Result());
                    })
                .WithName("GetPublicCatalogWork")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/catalog/sitemap.xml",
                    async (
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetPublicCatalogSitemapQuery(),
                            cancellationToken);

                        return result.Match(
                            sitemap => Results.Content(
                                ToSitemapXml(sitemap),
                                "application/xml",
                                Encoding.UTF8),
                            error => error.Result());
                    })
                .WithName("GetPublicCatalogSitemap")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/catalog/me/watchlist",
                    async (
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetMemberIdentity(principal, out var externalId, out var email))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new GetMyWatchlistQuery(externalId, email),
                            cancellationToken);

                        return result.Match(
                            watchlist => Results.Ok(ToResponse(watchlist)),
                            error => error.Result());
                    })
                .WithName("GetMyCatalogWatchlist")
                .RequireAuthorization();

            endpoints.MapPost(
                    "/catalog/me/watchlist",
                    async (
                        AddWatchlistItemRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetMemberIdentity(principal, out var externalId, out var email))
                        {
                            return Results.Unauthorized();
                        }

                        if (!Enum.TryParse<WatchlistItemScope>(
                                request.Scope,
                                ignoreCase: true,
                                out var scope) ||
                            !Enum.IsDefined(scope))
                        {
                            return DomainErrors.Watchlist.InvalidScope().Result();
                        }

                        var result = await mediator.Send(
                            new AddWatchlistItemCommand(
                                externalId,
                                email,
                                scope,
                                request.WorkId,
                                request.Isbn13),
                            cancellationToken);

                        return result.Match(
                            item => Results.Ok(ToResponse(item)),
                            error => error.Result());
                    })
                .WithName("AddCatalogWatchlistItem")
                .RequireAuthorization();

            endpoints.MapDelete(
                    "/catalog/me/watchlist/{itemId:guid}",
                    async (
                        Guid itemId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetMemberIdentity(principal, out var externalId, out var email))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new RemoveWatchlistItemCommand(externalId, email, itemId),
                            cancellationToken);

                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("RemoveCatalogWatchlistItem")
                .RequireAuthorization();

            endpoints.MapGet(
                    "/books/admin/dead-stock",
                    async (
                        int? minAgeMonths,
                        int? minQuantity,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetDeadStockQuery(
                                minAgeMonths ?? GetDeadStockQuery.DefaultMinAgeMonths,
                                minQuantity ?? GetDeadStockQuery.DefaultMinQuantity),
                            cancellationToken);

                        return result.Match(
                            deadStock => Results.Ok(ToResponse(deadStock)),
                            error => error.Result());
                    })
                .WithName("GetDeadStockCandidates")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/{isbn13}/metadata",
                    async (
                        string isbn13,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!Isbn13.TryCreate(isbn13, out var normalizedIsbn13))
                        {
                            return DomainErrors.Book.InvalidIsbn(isbn13).Result();
                        }

                        var result = await mediator.Send(
                            new GetBookMetadataQuery(normalizedIsbn13),
                            cancellationToken);

                        return result.Match(
                            metadata => Results.Ok(new BookMetadataResponse(
                                metadata.Isbn13,
                                metadata.Title,
                                metadata.Authors,
                                metadata.Publisher,
                                metadata.PublicationYear,
                                metadata.CoverUrl,
                                metadata.Source,
                                metadata.WorkId,
                                metadata.RetrievedAt,
                                metadata.CoverSource)),
                            error => error.Result());
                    })
                .WithName("GetBookMetadata")
                .AllowAnonymous();

            endpoints.MapGet(
                    "/scan/catalog/delta",
                    async (
                        DateTimeOffset? since,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetCatalogDeltaQuery(since?.UtcDateTime),
                            cancellationToken);

                        return result.Match(
                            delta => Results.Ok(ToResponse(delta)),
                            error => error.Result());
                    })
                .WithName("GetScanCatalogDelta")
                .RequireAuthorization("ScanVolunteer");

            endpoints.MapPost(
                    "/scan/sales",
                    async (
                        RegisterSaleRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var volunteerId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new RegisterSaleCommand(
                                request.Isbn,
                                request.Quantity,
                                request.OccurredAt,
                                volunteerId,
                                request.ClientGestureId),
                            cancellationToken);

                        return result.Match(
                            sale => Results.Ok(ToResponse(sale)),
                            error => error.Result());
                    })
                .WithName("RegisterBookSale")
                .RequireAuthorization("Caisse");

            endpoints.MapPost(
                    "/scan/sessions",
                    async (
                        OpenScanSessionRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var volunteerId))
                        {
                            return Results.Unauthorized();
                        }

                        if (!Enum.TryParse<ScanMode>(request.Mode, ignoreCase: true, out var mode) ||
                            !Enum.IsDefined(mode))
                        {
                            return DomainErrors.Book.InvalidScanMode().Result();
                        }

                        var result = await mediator.Send(
                            new OpenScanSessionCommand(
                                volunteerId,
                                mode,
                                ToAssoEventsId(request.TargetAssoEventsId),
                                request.ClientSessionId),
                            cancellationToken);

                        return result.Match(
                            session => Results.Ok(ToResponse(session)),
                            error => error.Result());
                    })
                .WithName("OpenScanSession")
                .RequireAuthorization("Tri");

            endpoints.MapPost(
                    "/scan/sessions/{scanSessionId:guid}/scans",
                    async (
                        Guid scanSessionId,
                        ScanBookRequest request,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new ScanBookCommand(
                                ScanSessionId.Create(scanSessionId),
                                request.Isbn,
                                request.Kept,
                                request.OccurredAt,
                                request.ClientGestureId),
                            cancellationToken);

                        return result.Match(
                            scan => Results.Ok(ToResponse(scan)),
                            error => error.Result());
                    })
                .WithName("ScanBook")
                .RequireAuthorization("Tri");

            endpoints.MapPost(
                    "/scan/sessions/{scanSessionId:guid}/close",
                    async (
                        Guid scanSessionId,
                        CloseScanSessionRequest request,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!Enum.TryParse<ScanCloseReason>(request.CloseReason, ignoreCase: true, out var closeReason) ||
                            !Enum.IsDefined(closeReason))
                        {
                            return Results.BadRequest(new {error = "A valid close reason is required."});
                        }

                        var result = await mediator.Send(
                            new CloseScanSessionCommand(
                                ScanSessionId.Create(scanSessionId),
                                closeReason),
                            cancellationToken);

                        return result.Match(
                            session => Results.Ok(ToResponse(session)),
                            error => error.Result());
                    })
                .WithName("CloseScanSession")
                .RequireAuthorization("Tri");
        });
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

    private static bool TryGetMemberIdentity(
        ClaimsPrincipal principal,
        out Guid externalId,
        out string email)
    {
        var externalIdValue = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(
                "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var emailValue = new[]
            {
                ClaimTypes.Email,
                "email",
                "emails",
                "preferred_username",
                ClaimTypes.Upn,
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
            }
            .Select(principal.FindFirst)
            .Select(claim => claim?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (Guid.TryParse(externalIdValue, out externalId) &&
            externalId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(emailValue) &&
            emailValue.Trim().Length <= 320)
        {
            email = emailValue.Trim();
            return true;
        }

        externalId = Guid.Empty;
        email = string.Empty;
        return false;
    }

    private static AssoEventsId? ToAssoEventsId(Guid? value)
    {
        if (value is null || value == Guid.Empty)
        {
            return null;
        }

        return AssoEventsId.Create(value.Value);
    }

    private static ScanCatalogDeltaResponse ToResponse(ScanCatalogDeltaResult result)
    {
        return new ScanCatalogDeltaResponse(
            result.GeneratedAt,
            result.NextWatermark,
            result.Books
                .Select(book => new ScanCatalogBookResponse(
                    book.Isbn13,
                    book.Title,
                    book.Authors,
                    book.WorkId,
                    book.QtyAvailable,
                    book.QtyAnnounced,
                    book.SalesCount,
                    book.IsWanted,
                    book.IsRare,
                    book.IsHidden,
                    book.UpdatedAt))
                .ToArray(),
            new ScanAssociationSettingsResponse(
                result.Settings.DuplicateThreshold,
                result.Settings.DemandSalesThreshold,
                result.Settings.DeadStockMinAgeDays,
                result.Settings.DeadStockMinQuantity,
                result.Settings.WatchlistMaxItems,
                result.Settings.AlertCooldownDays,
                result.Settings.SessionIdleTimeoutMinutes,
                result.Settings.AlertDelayMinutes,
                result.Settings.UpdatedAt));
    }

    private static ScanSessionResponse ToResponse(ScanSessionResult result)
    {
        return new ScanSessionResponse(
            result.ScanSessionId.Value,
            result.VolunteerId.Value,
            result.Mode.ToString(),
            result.TargetAssoEventsId?.Value,
            result.StartedAt,
            result.LastScanAt,
            result.LastSyncAt,
            result.LateArrivals,
            result.EndedAt,
            result.CloseReason?.ToString(),
            result.Status.ToString(),
            result.ScannedCount,
            result.KeptCount,
            result.RejectedCount);
    }

    private static ScanBookResponse ToResponse(ScanBookResult result)
    {
        return new ScanBookResponse(
            result.Isbn13,
            result.Verdict.Verdict.ToString(),
            result.QuantityAvailable,
            result.QuantityAnnounced,
            result.ScanSessionId.Value,
            result.MovementType.ToString(),
            result.AlreadyProcessed,
            result.ClockSuspect);
    }

    private static RegisterSaleResponse ToResponse(RegisterSaleResult result)
    {
        return new RegisterSaleResponse(
            result.Isbn13,
            result.SaleMovementId.Value,
            result.Quantity,
            result.QuantityAvailable,
            result.SalesCount,
            result.AssoEventsId?.Value,
            result.FairMatchStatus.ToString(),
            result.HadNoAvailableStock,
            result.HadUnreleasedAnnouncement,
            result.IsRare,
            result.ClockSuspect,
            result.AlreadyProcessed);
    }

    private static PublicCatalogSearchResponse ToResponse(PublicCatalogSearchResult result)
    {
        return new PublicCatalogSearchResponse(
            result.GeneratedAt,
            result.Books.Select(ToResponse).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.Genres);
    }

    private static DeadStockResponse ToResponse(DeadStockResult result)
    {
        return new DeadStockResponse(
            new DateTimeOffset(result.GeneratedAt, TimeSpan.Zero),
            result.MinAgeMonths,
            result.MinQuantity,
            result.Books
                .Select(book => new DeadStockBookResponse(
                    book.Isbn13,
                    book.Title,
                    book.Authors,
                    book.Publisher,
                    book.PublicationYear,
                    book.Genre,
                    book.QuantityAvailable,
                    new DateTimeOffset(book.FirstAvailableAt, TimeSpan.Zero)))
            .ToArray());
    }

    private static AddedWatchlistItemResponse ToResponse(AddedWatchlistItemResult result)
    {
        return new AddedWatchlistItemResponse(
            result.Id,
            result.Scope.ToString(),
            result.WorkId,
            result.Isbn13,
            result.AddedAt);
    }

    private static WatchlistResponse ToResponse(MyWatchlistResult result)
    {
        return new WatchlistResponse(
            result.GeneratedAt,
            result.AlertStatus.ToString(),
            result.BounceCount,
            result.Items.Select(item => new WatchlistItemResponse(
                    item.Id,
                    item.Scope.ToString(),
                    item.WorkId,
                    item.Isbn13,
                    item.Book is null ? null : ToResponse(item.Book),
                    item.AddedAt,
                    item.LastAlertAt))
                .ToArray());
    }

    private static PublicCatalogBookResponse ToResponse(PublicCatalogBookResult result)
    {
        return new PublicCatalogBookResponse(
            result.Isbn13,
            result.Title,
            result.Authors,
            result.Publisher,
            result.PublicationYear,
            result.PhysicalFormat,
            result.Language,
            result.Genre,
            result.WorkId,
            result.CoverUrl,
            result.QuantityAvailable,
            result.QuantityAnnounced,
            result.NextFairAt,
            result.LastAvailableAt,
            result.FirstSeenAt,
            result.UpdatedAt,
            result.IsRare,
            result.CoverSource);
    }

    private static PublicBookFairResponse ToResponse(PublicBookFairResult result)
    {
        return new PublicBookFairResponse(
            result.Id,
            result.Name,
            result.DateStart,
            result.DateEnd,
            result.OpenAt,
            result.CloseAt,
            result.RoadNumber,
            result.City,
            result.CityCode,
            result.Road);
    }

    private static bool TryParseAvailability(
        string? value,
        out PublicCatalogAvailabilityFilter availability)
    {
        availability = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "all" => PublicCatalogAvailabilityFilter.All,
            "available" or "available-now" or "available_now" =>
                PublicCatalogAvailabilityFilter.AvailableNow,
            "next" or "next-fair" or "next_fair" =>
                PublicCatalogAvailabilityFilter.NextBookFair,
            _ => default
        };

        return string.IsNullOrWhiteSpace(value) ||
               value.Trim().ToLowerInvariant() is "all" or "available" or "available-now" or
                   "available_now" or "next" or "next-fair" or "next_fair";
    }

    private static bool TryParseSort(string? value, out PublicCatalogSortOrder sort)
    {
        sort = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "relevance" => PublicCatalogSortOrder.Relevance,
            "recent" or "recently-added" or "recently_added" =>
                PublicCatalogSortOrder.RecentlyAdded,
            _ => default
        };

        return string.IsNullOrWhiteSpace(value) ||
               value.Trim().ToLowerInvariant() is "relevance" or "recent" or "recently-added" or
                   "recently_added";
    }

    private static string ToSitemapXml(PublicCatalogSitemapResult sitemap)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var entry in sitemap.Entries)
        {
            builder.Append("<url><loc>https://livres.volepapillondamour.fr");
            builder.Append(System.Security.SecurityElement.Escape(entry.UrlPath));
            builder.Append("</loc><lastmod>");
            builder.Append(entry.LastModified.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            builder.Append("</lastmod></url>");
        }

        builder.Append("</urlset>");
        return builder.ToString();
    }
}
