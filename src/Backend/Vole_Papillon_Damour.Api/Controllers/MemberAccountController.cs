using System.Security.Claims;
using MediatR;
using Vole_Papillon_Damour.Api.Common;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.MergeSelection;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.RemoveSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Application.MemberSelection.Queries.GetMySelection;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.CancelNotFoundReport;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.ReportNotFound;
using Vole_Papillon_Damour.Application.MemberCards.Commands.RotateMyCard;
using Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;
using Vole_Papillon_Damour.Application.Purchases.Queries.GetMyPurchases;
using Vole_Papillon_Damour.Contracts.MemberSelection;
using Vole_Papillon_Damour.Contracts.MemberCards;
using Vole_Papillon_Damour.Contracts.Purchases;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class MemberAccountController
{
    public static IApplicationBuilder UseMemberAccountController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/catalog/me/card",
                    async (
                        ClaimsPrincipal principal,
                        HttpContext httpContext,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        httpContext.Response.Headers.CacheControl = "no-store";
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new GetMyCardQuery(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName),
                            cancellationToken);
                        return result.Match(
                            card => Results.Ok(ToResponse(card)),
                            error => error.Result());
                    })
                .WithName("GetMyMemberCard")
                .RequireAuthorization();

            endpoints.MapPost(
                    "/catalog/me/card/rotate",
                    async (
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new RotateMyCardCommand(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName),
                            cancellationToken);
                        return result.Match(
                            card => Results.Ok(ToResponse(card)),
                            error => error.Result());
                    })
                .WithName("RotateMyMemberCard")
                .RequireAuthorization();

            endpoints.MapGet(
                    "/catalog/me/selection",
                    async (
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new GetMySelectionQuery(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName),
                            cancellationToken);
                        return result.Match(
                            selection => Results.Ok(ToResponse(selection)),
                            error => error.Result());
                    })
                .WithName("GetMyMemberSelection")
                .RequireAuthorization();

            endpoints.MapPost(
                    "/catalog/me/selection/{id:guid}/not-found-report",
                    async (
                        Guid id,
                        ReportNotFoundRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new ReportNotFoundCommand(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName,
                                id,
                                request.Location,
                                request.Comment),
                            cancellationToken);
                        return result.Match(
                            report => report.AlreadyOpen
                                ? Results.Ok(new NotFoundReportCreatedResponse(
                                    report.ReportId, report.ReportedAt, report.AlreadyOpen))
                                : Results.Json(
                                    new NotFoundReportCreatedResponse(
                                        report.ReportId, report.ReportedAt, report.AlreadyOpen),
                                    statusCode: StatusCodes.Status201Created),
                            error => error.Result());
                    })
                .WithName("ReportNotFoundBookSelection")
                .RequireAuthorization();

            endpoints.MapDelete(
                    "/catalog/me/not-found-reports/{reportId:guid}",
                    async (
                        Guid reportId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new CancelNotFoundReportCommand(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName,
                                reportId),
                            cancellationToken);
                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("CancelNotFoundReport")
                .RequireAuthorization();

            endpoints.MapGet(
                    "/catalog/me/purchases",
                    async (
                        string? cursor,
                        int? limit,
                        ClaimsPrincipal principal,
                        HttpContext httpContext,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        httpContext.Response.Headers.CacheControl = "no-store";
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new GetMyPurchasesQuery(
                                identity.ExternalId.ToString("D"),
                                identity.Email,
                                identity.FirstName,
                                identity.LastName,
                                cursor,
                                limit ?? 10),
                            cancellationToken);
                        return result.Match(
                            purchases => Results.Ok(ToResponse(purchases)),
                            error => error.Result());
                    })
                .WithName("GetMyMemberPurchases")
                .RequireAuthorization();

            endpoints.MapPost(
                    "/catalog/me/selection",
                    async (
                        AddSelectionItemRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new AddSelectionItemCommand(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName,
                                request.Isbn13,
                                request.RareBookId),
                            cancellationToken);
                        return result.Match(
                            added => Results.Ok(new { added.Id, added.AlreadyPresent }),
                            error => error.Result());
                    })
                .WithName("AddMemberSelectionItem")
                .RequireAuthorization();

            endpoints.MapDelete(
                    "/catalog/me/selection/{id:guid}",
                    async (
                        Guid id,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new RemoveSelectionItemCommand(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName,
                                id),
                            cancellationToken);
                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("RemoveMemberSelectionItem")
                .RequireAuthorization();

            endpoints.MapPatch(
                    "/catalog/me/selection/{id:guid}",
                    (ClaimsPrincipal principal) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out _))
                        {
                            return Results.Unauthorized();
                        }

                        return Results.Problem(
                            statusCode: StatusCodes.Status410Gone,
                            title: "Selection statuses were retired",
                            detail: "Use POST /catalog/me/selection/{id}/not-found-report.");
                    })
                .WithName("SetMemberSelectionItemStatus")
                .RequireAuthorization();

            endpoints.MapPost(
                    "/catalog/me/selection/merge",
                    async (
                        MergeSelectionRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        if (request.Entries is null || request.Entries.Any(entry => entry is null))
                        {
                            return Results.BadRequest();
                        }

                        var entries = request.Entries
                            .Select(entry => new MergeSelectionEntry(
                                entry.Isbn13,
                                entry.RareBookId,
                                entry.AddedAt.UtcDateTime))
                            .ToArray();
                        var result = await mediator.Send(
                            new MergeSelectionCommand(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName,
                                entries),
                            cancellationToken);
                        return result.Match(
                            merged => Results.Ok(new
                            {
                                merged.Added,
                                merged.AlreadyPresent,
                                merged.Rejected
                            }),
                            error => error.Result());
                    })
                .WithName("MergeMemberSelection")
                .RequireAuthorization();
        });
    }

    private static MySelectionResponse ToResponse(MySelectionResult selection) =>
        new(
            selection.GeneratedAt,
            selection.NextFair is null
                ? null
                : new NextFairSummaryResponse(selection.NextFair.Id, selection.NextFair.StartsAt),
            selection.Items.Select(item => new MySelectionItemResponse(
                item.Id,
                item.Kind,
                item.Isbn13,
                item.RareBookId,
                item.RareBookSlug,
                item.Title,
                item.Authors,
                item.Publisher,
                item.PublicationYear,
                item.PhysicalFormat,
                item.CoverUrl,
                item.Availability.ToString(),
                item.AvailabilityCheckedAt,
                item.Status.ToString(),
                item.AddedAt,
                item.PurchasedAt,
                item.NotFoundReport is null
                    ? null
                    : new NotFoundReportSummaryResponse(
                        item.NotFoundReport.Id,
                        item.NotFoundReport.Status,
                        item.NotFoundReport.ReportedAt,
                        item.NotFoundReport.ClosedAt))).ToArray());

    private static MemberCardResponse ToResponse(MyCardResult card) =>
        new(card.QrPayload, card.RecoveryCode, card.DisplayLabel, card.IssuedAt);

    private static MyPurchasesResponse ToResponse(MyPurchasesPage page) =>
        new(
            page.Passages.Select(passage => new PurchasePassageResponse(
                passage.Id,
                passage.Reference,
                passage.OccurredAt,
                passage.FairId,
                passage.FairLabel,
                passage.ActiveBookCount,
                passage.Lines.Select(line => new PurchaseLineResponse(
                    line.Id,
                    line.Kind,
                    line.Isbn13,
                    line.RareBookId,
                    line.Title,
                    line.Authors,
                    line.Publisher,
                    line.PublicationYear,
                    line.PhysicalFormat,
                    line.Quantity,
                    line.State,
                    line.CurrentCoverUrl)).ToArray())).ToArray(),
            page.NextCursor);
}

