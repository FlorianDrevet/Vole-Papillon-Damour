using System.Security.Claims;
using MediatR;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.GetCatalogDelta;
using Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;
using Vole_Papillon_Damour.Contracts.Books.Requests;
using Vole_Papillon_Damour.Contracts.Books.Responses;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class BookController
{
    public static IApplicationBuilder UseBookController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
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
                                metadata.RetrievedAt)),
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
                .RequireAuthorization("Tri");

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
}
