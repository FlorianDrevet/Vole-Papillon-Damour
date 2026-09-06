using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Books.Commands.AdjustQuantity;
using Vole_Papillon_Damour.Application.Books.Commands.AssociationSettings;
using Vole_Papillon_Damour.Application.Books.Commands.BookFlags;
using Vole_Papillon_Damour.Application.Books.Commands.CancelBookAlerts;
using Vole_Papillon_Damour.Application.Books.Commands.DeleteBook;
using Vole_Papillon_Damour.Application.Books.Commands.ForceBookAlerts;
using Vole_Papillon_Damour.Application.Books.Commands.ReassignSessionMode;
using Vole_Papillon_Damour.Application.Books.Commands.UpdateBookMetadata;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Books.Queries.GetAssociationSettings;
using Vole_Papillon_Damour.Contracts.Books.Requests;
using Vole_Papillon_Damour.Contracts.Books.Responses;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class BookAdministrationController
{
    public static IApplicationBuilder UseBookAdministrationController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/books/admin/overview",
                    async (
                        DateTimeOffset? from,
                        DateTimeOffset? to,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetCatalogAdminOverviewQuery(from, to),
                            cancellationToken);
                        return result.Match(
                            overview => Results.Ok(ToResponse(overview)),
                            error => error.Result());
                    })
                .WithName("GetCatalogAdministrationOverview")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/books",
                    async (
                        string? search,
                        string? metadataStatus,
                        bool? rare,
                        bool? hidden,
                        bool? undated,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminBooksQuery(
                                search,
                                metadataStatus,
                                rare,
                                hidden,
                                page ?? 1,
                                pageSize ?? 50,
                                undated),
                            cancellationToken);
                        return result.Match(
                            books => Results.Ok(ToResponse(books)),
                            error => error.Result());
                    })
                .WithName("GetAdminBooks")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/books/{isbn13}",
                    async (
                        string isbn13,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminBookQuery(isbn13),
                            cancellationToken);
                        return result.Match(
                            book => Results.Ok(ToResponse(book)),
                            error => error.Result());
                    })
                .WithName("GetAdminBook")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/books",
                    async (
                        AddAdminBookRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        if (!TryParseMetadataFields(request.Fields, request, out var fields))
                        {
                            return Results.BadRequest(new { error = "Fields contains an unsupported metadata field." });
                        }

                        var result = await mediator.Send(
                            new AddBookCommand(
                                request.Isbn13,
                                request.QuantityAvailable,
                                request.Note,
                                userId,
                                request.Title,
                                request.Authors,
                                request.Publisher,
                                request.PublicationYear,
                                request.PhysicalFormat,
                                request.Language,
                                request.Genre,
                                request.CoverBlobRef,
                                request.WorkId,
                                fields),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Created(
                                $"/books/admin/books/{operation.Isbn13}",
                                ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("AddAdminBook")
                .RequireAuthorization("Administration");

            endpoints.MapPatch(
                    "/books/admin/books/{isbn13}/metadata",
                    async (
                        string isbn13,
                        UpdateAdminBookMetadataRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId) ||
                            !TryParseMetadataFields(request.Fields, out var fields))
                        {
                            return Results.BadRequest(new { error = "A valid administrator and metadata fields are required." });
                        }

                        var result = await mediator.Send(
                            new UpdateBookMetadataCommand(
                                isbn13,
                                request.Title,
                                request.Authors,
                                request.Publisher,
                                request.PublicationYear,
                                request.PhysicalFormat,
                                request.Language,
                                request.Genre,
                                request.CoverBlobRef,
                                fields,
                                userId,
                                request.WorkId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(new
                            {
                                operation.Isbn13,
                                metadataStatus = operation.MetadataStatus.ToString(),
                                metadataSource = operation.MetadataSource.ToString(),
                                operation.ManuallyEditedFields,
                                updatedAt = new DateTimeOffset(operation.UpdatedAt, TimeSpan.Zero),
                                operation.Changed
                            }),
                            error => error.Result());
                    })
                .WithName("UpdateAdminBookMetadata")
                .RequireAuthorization("Administration");

            endpoints.MapPatch(
                    "/books/admin/books/{isbn13}/quantity",
                    async (
                        string isbn13,
                        CorrectBookQuantityRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new AdjustQuantityCommand(isbn13, request.QuantityAvailable, request.Note, userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(new AdminQuantityCorrectionResponse(
                                operation.Isbn13,
                                operation.PreviousQuantityAvailable,
                                operation.QuantityAvailable,
                                operation.Delta,
                                operation.Changed,
                                operation.MovementId?.Value)),
                            error => error.Result());
                    })
                .WithName("CorrectAdminBookQuantity")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/books/{isbn13}/withdrawals",
                    async (
                        string isbn13,
                        WithdrawBookRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new WithdrawBookCommand(isbn13, request.Quantity, request.Note, userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("WithdrawAdminBook")
                .RequireAuthorization("Administration");

            endpoints.MapPatch(
                    "/books/admin/announcements/{announcementId:guid}/quantity",
                    async (
                        Guid announcementId,
                        CorrectAnnouncementQuantityRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new AdjustAnnouncementQuantityCommand(
                                announcementId,
                                request.Quantity,
                                request.Note,
                                userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("CorrectAdminAnnouncementQuantity")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/books/{isbn13}/rare",
                    async (
                        string isbn13,
                        bool isRare,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new MarkBookRareCommand(isbn13, isRare, userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(new
                            {
                                operation.Isbn13,
                                operation.IsRare,
                                operation.IsHiddenFromCatalog,
                                operation.Changed
                            }),
                            error => error.Result());
                    })
                .WithName("SetAdminBookRare")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/books/{isbn13}/visibility",
                    async (
                        string isbn13,
                        bool hidden,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new HideBookCommand(isbn13, hidden, userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(new
                            {
                                operation.Isbn13,
                                operation.IsRare,
                                operation.IsHiddenFromCatalog,
                                operation.Changed
                            }),
                            error => error.Result());
                    })
                .WithName("SetAdminBookVisibility")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/books/{sourceIsbn13}/merge",
                    async (
                        string sourceIsbn13,
                        MergeBooksRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new MergeBooksCommand(
                                sourceIsbn13,
                                request.TargetIsbn13,
                                request.Note,
                                userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("MergeAdminBooks")
                .RequireAuthorization("Administration");

            endpoints.MapDelete(
                    "/books/admin/books/{isbn13}",
                    async (
                        string isbn13,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new DeleteBookCommand(isbn13, userId),
                            cancellationToken);
                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("DeleteAdminBook")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/fairs",
                    async (
                        bool? includeCancelled,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminBookFairsQuery(
                                includeCancelled == true,
                                page ?? 1,
                                pageSize ?? 50),
                            cancellationToken);
                        return result.Match(
                            fairs => Results.Ok(ToResponse(fairs)),
                            error => error.Result());
                    })
                .WithName("GetAdminBookFairs")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/fairs/{fairId:guid}/stats",
                    async (
                        Guid fairId,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminFairStatsQuery(fairId),
                            cancellationToken);
                        return result.Match(
                            stats => Results.Ok(ToResponse(stats)),
                            error => error.Result());
                    })
                .WithName("GetAdminBookFairStats")
                .RequireAuthorization("Administration");

            endpoints.MapPut(
                    "/books/admin/fairs/{fairId:guid}/revenue",
                    async (
                        Guid fairId,
                        SetBookFairRevenueRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new SetBookFairRevenueCommand(
                                AssoEventsId.Create(fairId),
                                request.Revenue,
                                userId),
                            cancellationToken);
                        return result.Match(
                            fair => Results.Ok(ToResponse(fair)),
                            error => error.Result());
                    })
                .WithName("SetAdminBookFairRevenue")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/sessions",
                    async (
                        string? status,
                        DateTimeOffset? from,
                        DateTimeOffset? to,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminScanSessionsQuery(
                                status,
                                from,
                                to,
                                page ?? 1,
                                pageSize ?? 50),
                            cancellationToken);
                        return result.Match(
                            sessions => Results.Ok(ToResponse(sessions)),
                            error => error.Result());
                    })
                .WithName("GetAdminScanSessions")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/sessions/{scanSessionId:guid}",
                    async (
                        Guid scanSessionId,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminScanSessionQuery(scanSessionId),
                            cancellationToken);
                        return result.Match(
                            session => Results.Ok(ToResponse(session)),
                            error => error.Result());
                    })
                .WithName("GetAdminScanSession")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/sessions/{scanSessionId:guid}/movements/{movementId:guid}/remove",
                    async (
                        Guid scanSessionId,
                        Guid movementId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new RemoveScanSessionMovementCommand(
                                ScanSessionId.Create(scanSessionId),
                                movementId,
                                userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("RemoveAdminScanSessionMovement")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/sessions/{scanSessionId:guid}/reassign",
                    async (
                        Guid scanSessionId,
                        ReassignAdminSessionRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId) ||
                            !Enum.TryParse<ScanMode>(request.Mode, true, out var mode) ||
                            !Enum.IsDefined(mode))
                        {
                            return Results.BadRequest(new { error = "A valid mode and administrator are required." });
                        }

                        var result = await mediator.Send(
                            new ReassignSessionModeCommand(
                                ScanSessionId.Create(scanSessionId),
                                mode,
                                request.TargetAssoEventsId is { } fairId && fairId != Guid.Empty
                                    ? AssoEventsId.Create(fairId)
                                    : null,
                                userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(new AdminScanSessionOperationResponse(
                                operation.ScanSessionId.Value,
                                operation.ReversedMovementCount + operation.ReplayedMovementCount,
                                0,
                                Changed: true)),
                            error => error.Result());
                    })
                .WithName("ReassignAdminScanSession")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/sessions/{scanSessionId:guid}/cancel",
                    async (
                        Guid scanSessionId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new CancelScanSessionCommand(ScanSessionId.Create(scanSessionId), userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("CancelAdminScanSession")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/sessions/{scanSessionId:guid}/alerts/cancel",
                    async (
                        Guid scanSessionId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new CancelBookAlertsCommand(ScanSessionId.Create(scanSessionId), userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(new AdminScanSessionOperationResponse(
                                operation.ScanSessionId.Value,
                                0,
                                operation.AffectedCount,
                                operation.AffectedCount > 0)),
                            error => error.Result());
                    })
                .WithName("CancelAdminScanSessionAlerts")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/sessions/{scanSessionId:guid}/alerts/force",
                    async (
                        Guid scanSessionId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new ForceBookAlertsCommand(ScanSessionId.Create(scanSessionId), userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(new AdminScanSessionOperationResponse(
                                operation.ScanSessionId.Value,
                                0,
                                operation.AffectedCount,
                                operation.AffectedCount > 0)),
                            error => error.Result());
                    })
                .WithName("ForceAdminScanSessionAlerts")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/alerts",
                    async (
                        string? status,
                        Guid? scanSessionId,
                        Guid? memberId,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminAlertsQuery(
                                status,
                                scanSessionId,
                                memberId,
                                page ?? 1,
                                pageSize ?? 50),
                            cancellationToken);
                        return result.Match(
                            alerts => Results.Ok(ToResponse(alerts)),
                            error => error.Result());
                    })
                .WithName("GetAdminBookAlerts")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/alerts/{messageId:guid}/cancel",
                    async (
                        Guid messageId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new CancelBookAlertMessageCommand(messageId, userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("CancelAdminBookAlert")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/alerts/{messageId:guid}/force",
                    async (
                        Guid messageId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new ForceBookAlertMessageCommand(messageId, userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("ForceAdminBookAlert")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/members",
                    async (
                        string? search,
                        string? alertStatus,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminMembersQuery(
                                search,
                                alertStatus,
                                page ?? 1,
                                pageSize ?? 50),
                            cancellationToken);
                        return result.Match(
                            members => Results.Ok(ToResponse(members)),
                            error => error.Result());
                    })
                .WithName("GetAdminMembers")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/members/{memberId:guid}",
                    async (
                        Guid memberId,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminMemberQuery(memberId),
                            cancellationToken);
                        return result.Match(
                            member => Results.Ok(ToResponse(member)),
                            error => error.Result());
                    })
                .WithName("GetAdminMember")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/members/{memberId:guid}/block",
                    async (
                        Guid memberId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        return await SetMemberStatusAsync(memberId, true, principal, mediator, cancellationToken);
                    })
                .WithName("BlockAdminMember")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/members/{memberId:guid}/unblock",
                    async (
                        Guid memberId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        return await SetMemberStatusAsync(memberId, false, principal, mediator, cancellationToken);
                    })
                .WithName("UnblockAdminMember")
                .RequireAuthorization("Administration");

            endpoints.MapDelete(
                    "/books/admin/members/{memberId:guid}",
                    async (
                        Guid memberId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new DeleteMemberCommand(UserId.Create(memberId), userId),
                            cancellationToken);
                        return result.Match(
                            operation => Results.Ok(ToResponse(operation)),
                            error => error.Result());
                    })
                .WithName("DeleteAdminMember")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/settings",
                    async (IMediator mediator, CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAssociationSettingsQuery(),
                            cancellationToken);
                        return result.Match(
                            settings => Results.Ok(ToResponse(settings)),
                            error => error.Result());
                    })
                .WithName("GetAdminAssociationSettings")
                .RequireAuthorization("Administration");

            endpoints.MapPut(
                    "/books/admin/settings",
                    async (
                        UpdateAdminAssociationSettingsRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new UpdateAssociationSettingsCommand(
                                request.DuplicateThreshold,
                                request.DemandSalesThreshold,
                                request.DeadStockMinAgeDays,
                                request.DeadStockMinQuantity,
                                request.WatchlistMaxItems,
                                request.AlertCooldownDays,
                                request.SessionIdleTimeoutMinutes,
                                request.AlertDelayMinutes,
                                userId),
                            cancellationToken);
                        return result.Match(
                            settings => Results.Ok(ToResponse(settings)),
                            error => error.Result());
                    })
                .WithName("UpdateAdminAssociationSettings")
                .RequireAuthorization("Administration");
        });
    }

    private static async Task<IResult> SetMemberStatusAsync(
        Guid memberId,
        bool blocked,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(
            new SetMemberAlertStatusCommand(UserId.Create(memberId), blocked, userId),
            cancellationToken);
        return result.Match(
            operation => Results.Ok(ToResponse(operation)),
            error => error.Result());
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

    private static bool TryParseMetadataFields(
        IReadOnlyCollection<string>? values,
        AddAdminBookRequest request,
        out IReadOnlyCollection<BookMetadataField> fields)
    {
        if (values is null)
        {
            values = BuildInferredFields(request);
        }

        return TryParseMetadataFields(values, out fields);
    }

    private static bool TryParseMetadataFields(
        IReadOnlyCollection<string> values,
        out IReadOnlyCollection<BookMetadataField> fields)
    {
        var parsed = new List<BookMetadataField>();
        foreach (var value in values ?? [])
        {
            if (!Enum.TryParse<BookMetadataField>(value, true, out var field) ||
                !Enum.IsDefined(field))
            {
                fields = [];
                return false;
            }

            parsed.Add(field);
        }

        fields = parsed.Distinct().ToArray();
        return true;
    }

    private static IReadOnlyCollection<string> BuildInferredFields(AddAdminBookRequest request)
    {
        var fields = new List<string>();
        if (request.Title is not null) fields.Add(nameof(BookMetadataField.Title));
        if (request.Authors is not null) fields.Add(nameof(BookMetadataField.Authors));
        if (request.Publisher is not null) fields.Add(nameof(BookMetadataField.Publisher));
        if (request.PublicationYear is not null) fields.Add(nameof(BookMetadataField.PublicationYear));
        if (request.PhysicalFormat is not null) fields.Add(nameof(BookMetadataField.PhysicalFormat));
        if (request.Language is not null) fields.Add(nameof(BookMetadataField.Language));
        if (request.Genre is not null) fields.Add(nameof(BookMetadataField.Genre));
        if (request.CoverBlobRef is not null) fields.Add(nameof(BookMetadataField.CoverBlobRef));
        if (request.WorkId is not null) fields.Add(nameof(BookMetadataField.WorkId));
        return fields;
    }

    private static CatalogAdminOverviewResponse ToResponse(CatalogAdminOverviewResult result) =>
        new(
            result.GeneratedAt,
            ToResponse(result.CurrentPeriod),
            ToResponse(result.PreviousPeriod),
            new AdminStockSummaryResponse(
                result.Stock.AvailableQuantity,
                result.Stock.AvailableTitles,
                result.Stock.AnnouncedQuantity,
                result.Stock.AnnouncedTitles),
            result.LastFair is null ? null : ToResponse(result.LastFair),
            result.DeadStockCount,
            result.RareQueueCount,
            result.MetadataMissingCount,
            result.UndatedAnnouncementCount,
            result.InventoryDriftTitleCount,
            result.InventoryDriftQuantity,
            new AdminAlertQueueSummaryResponse(
                result.PendingAlerts.PendingCount,
                result.PendingAlerts.OldestDueAt,
                result.PendingAlerts.NextDueAt));

    private static AdminPeriodMetricsResponse ToResponse(AdminPeriodMetricsResult result) =>
        new(result.From, result.To, result.ScannedCount, result.KeptCount, result.RejectedCount,
            result.SoldQuantity, result.SoldTitles);

    private static AdminFairSummaryResponse ToResponse(AdminFairSummaryResult result) =>
        new(result.Id, result.Name, result.DateStart, result.DateEnd, result.SoldQuantity,
            result.SoldTitles, result.Revenue);

    private static AdminBookPageResponse ToResponse(AdminBookPageResult result) =>
        new(result.GeneratedAt, result.Books.Select(ToResponse).ToArray(), result.TotalCount,
            result.Page, result.PageSize);

    private static AdminBookResponse ToResponse(AdminBookResult result) =>
        new(result.Isbn13, result.WorkId, result.Title, result.Authors, result.Publisher,
            result.PublicationYear, result.PhysicalFormat, result.Language, result.Genre,
            result.MetadataStatus, result.MetadataSource, result.ManuallyEditedFields,
            result.QuantityAvailable, result.QuantityAnnounced, result.SalesCount,
            result.RejectionCount, result.IsRare, result.IsHidden, result.RedirectedToIsbn13,
            result.CoverUrl, result.FirstSeenAt, result.LastAvailableAt, result.UpdatedAt,
            result.Announcements.Select(ToResponse).ToArray(),
            result.Movements.Select(ToResponse).ToArray());

    private static AdminAnnouncementResponse ToResponse(AdminAnnouncementResult result) =>
        new(result.Id, result.Isbn13, result.FairId, result.Quantity, result.Status,
            result.CreatedAt, result.ReleasedAt, result.ScanSessionId);

    private static AdminBookMovementResponse ToResponse(AdminBookMovementResult result) =>
        new(result.Id, result.Isbn13, result.Type, result.Quantity, result.OccurredAt,
            result.ReceivedAt, result.ClockSuspect, result.ScanSessionId, result.VolunteerId,
            result.FairId, result.Note, result.ClientGestureId, result.ReversalOfMovementId);

    private static AdminBookOperationResponse ToResponse(AdminBookOperationResult result) =>
        new(result.Isbn13, result.QuantityAvailable, result.QuantityAnnounced, result.Changed,
            result.MovementId);

    private static AdminFairPageResponse ToResponse(AdminFairPageResult result) =>
        new(result.GeneratedAt, result.Fairs.Select(ToResponse).ToArray(), result.TotalCount,
            result.Page, result.PageSize);

    private static AdminFairResponse ToResponse(AdminFairResult result) =>
        new(result.Id, result.Name, result.DateStart, result.DateEnd, result.IsCancelled,
            result.Revenue);

    private static AdminFairStatsResponse ToResponse(AdminFairStatsResult result) =>
        new(ToResponse(result.Fair), result.SoldQuantity, result.SoldTitles, result.Revenue,
            result.AverageBasket,
            result.SalesByGenre.Select(item => new AdminGenreSalesResponse(item.Genre, item.Quantity)).ToArray(),
            result.TopBooks.Select(item => new AdminTopBookResponse(item.Isbn13, item.Title, item.Authors, item.Genre, item.Quantity)).ToArray(),
            result.DailySales.Select(item => new AdminDailySalesResponse(item.Day, item.Quantity)).ToArray(),
            result.PreviousFairs.Select(item => new AdminFairComparisonResponse(item.FairId, item.Name, item.DateStart, item.SoldQuantity, item.Revenue)).ToArray());

    private static AdminScanSessionPageResponse ToResponse(AdminScanSessionPageResult result) =>
        new(result.GeneratedAt, result.Sessions.Select(ToResponse).ToArray(), result.TotalCount,
            result.Page, result.PageSize);

    private static AdminScanSessionResponse ToResponse(AdminScanSessionResult result) =>
        new(result.Id, result.VolunteerId, result.VolunteerName, result.Mode, result.FairId,
            result.FairName, result.StartedAt, result.LastScanAt, result.LastSyncAt,
            result.EndedAt, result.CloseReason, result.Status, result.ScannedCount,
            result.KeptCount, result.RejectedCount, result.AlertCount, result.PendingAlertCount,
            result.NextAlertDueAt, result.Movements.Select(ToResponse).ToArray());

    private static AdminScanSessionOperationResponse ToResponse(AdminScanSessionOperationResult result) =>
        new(result.ScanSessionId, result.AffectedMovementCount, result.AffectedAlertCount, result.Changed);

    private static AdminAlertPageResponse ToResponse(AdminAlertPageResult result) =>
        new(result.GeneratedAt, result.Alerts.Select(item => new AdminAlertResponse(
                item.Id, item.ScanSessionId, item.MemberId, item.Status, item.ItemCount,
                item.Attempts, item.CreatedAt, item.DueAt, item.SentAt, item.LastError))
            .ToArray(), result.TotalCount, result.Page, result.PageSize);

    private static AdminMemberPageResponse ToResponse(AdminMemberPageResult result) =>
        new(result.GeneratedAt, result.Members.Select(ToResponse).ToArray(), result.TotalCount,
            result.Page, result.PageSize);

    private static AdminMemberSummaryResponse ToResponse(AdminMemberSummaryResult result) =>
        new(result.Id, result.ExternalId, result.Email, result.DisplayName, result.CreatedAt,
            result.LastSeenAt, result.AnonymizedAt, result.AlertStatus, result.BounceCount,
            result.WatchlistItemCount, result.AlertHistoryCount);

    private static AdminMemberDetailResponse ToResponse(AdminMemberDetailResult result) =>
        new(ToResponse(result.Member),
            result.Watchlist.Select(item => new AdminMemberWatchlistItemResponse(
                item.Id, item.Scope, item.WorkId, item.Isbn13, item.Title, item.Authors,
                item.QuantityAvailable, item.QuantityAnnounced, item.AddedAt, item.LastAlertAt)).ToArray(),
            result.Alerts.Select(item => new AdminMemberAlertHistoryResponse(
                item.Id, item.Isbn13, item.Title, item.SentAt, item.OutboxMessageId)).ToArray());

    private static AdminMemberOperationResponse ToResponse(AdminMemberOperationResult result) =>
        new(result.MemberId, result.AlertStatus, result.Changed, result.DeletionCompleted);

    private static AdminAssociationSettingsResponse ToResponse(AssociationSettingsResult result) =>
        new(result.DuplicateThreshold, result.DemandSalesThreshold, result.DeadStockMinAgeDays,
            result.DeadStockMinQuantity, result.WatchlistMaxItems, result.AlertCooldownDays,
            result.SessionIdleTimeoutMinutes, result.AlertDelayMinutes,
            new DateTimeOffset(result.UpdatedAt, TimeSpan.Zero), result.UpdatedBy.Value);

    private static AdminQuantityCorrectionResponse ToResponse(AdjustQuantityResult result) =>
        new(result.Isbn13, result.PreviousQuantityAvailable, result.QuantityAvailable,
            result.Delta, result.Changed, result.MovementId?.Value);

    private static AdminAlertOperationResponse ToResponse(AdminAlertOperationResult result) =>
        new(result.MessageId, result.Status, result.Changed);
}
