using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.DismissNotFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.MarkFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.WithdrawNotFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetClosedNotFoundReports;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportSummary;
using Vole_Papillon_Damour.Contracts.NotFoundReports;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class NotFoundReportAdministrationController
{
    public static IApplicationBuilder UseNotFoundReportAdministrationController(
        this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/books/admin/not-found-reports",
                    async (
                        string? kind,
                        string? sort,
                        int? page,
                        int? pageSize,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetNotFoundReportQueueQuery(
                                kind ?? "all",
                                sort ?? "most-reported",
                                page ?? 1,
                                pageSize ?? 50,
                                IsRareBooksOnly(principal)),
                            cancellationToken);
                        return result.Match(
                            queue => Results.Ok(ToResponse(queue)),
                            error => error.Result());
                    })
                .WithName("GetNotFoundReportQueue")
                .RequireAuthorization("RareBooks");

            endpoints.MapGet(
                    "/books/admin/not-found-reports/closed",
                    async (
                        int? page,
                        int? pageSize,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetClosedNotFoundReportsQuery(
                                page ?? 1,
                                pageSize ?? 50,
                                IsRareBooksOnly(principal)),
                            cancellationToken);
                        return result.Match(
                            reports => Results.Ok(ToResponse(reports)),
                            error => error.Result());
                    })
                .WithName("GetClosedNotFoundReports")
                .RequireAuthorization("RareBooks");

            endpoints.MapGet(
                    "/books/admin/not-found-reports/summary",
                    async (
                        string? isbn13,
                        Guid? rareBookId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetNotFoundReportSummaryQuery(
                                isbn13,
                                rareBookId,
                                IsRareBooksOnly(principal)),
                            cancellationToken);
                        return result.Match(
                            summary => Results.Ok(ToResponse(summary)),
                            error => error.Result());
                    })
                .WithName("GetNotFoundReportSummary")
                .RequireAuthorization("RareBooks");

            endpoints.MapPost(
                    "/books/admin/not-found-reports/editions/{isbn13}/found",
                    async (
                        string isbn13,
                        NotFoundReportFoundRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new MarkNotFoundTargetFoundCommand(
                                new NotFoundReportTargetRef("edition", isbn13), request.Note, userId),
                            cancellationToken);
                        return result.Match(
                            closure => Results.Ok(ToResponse(closure)),
                            error => error.Result());
                    })
                .WithName("MarkEditionNotFoundReportsFound")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/not-found-reports/editions/{isbn13}/withdrawal",
                    async (
                        string isbn13,
                        NotFoundReportWithdrawalRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        if (!TryParseReason(request.Reason, out var reason))
                        {
                            return Results.BadRequest(new { error = "The withdrawal reason is not recognised." });
                        }

                        var result = await mediator.Send(
                            new WithdrawNotFoundTargetCommand(
                                new NotFoundReportTargetRef("edition", isbn13),
                                request.QuantityFound,
                                reason,
                                request.Note,
                                userId),
                            cancellationToken);
                        return result.Match(
                            closure => Results.Ok(ToResponse(closure)),
                            error => error.Result());
                    })
                .WithName("WithdrawEditionNotFoundReports")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/not-found-reports/editions/{isbn13}/dismissal",
                    async (
                        string isbn13,
                        NotFoundReportDismissalRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new DismissNotFoundTargetCommand(
                                new NotFoundReportTargetRef("edition", isbn13), request.Note, userId),
                            cancellationToken);
                        return result.Match(
                            closure => Results.Ok(ToResponse(closure)),
                            error => error.Result());
                    })
                .WithName("DismissEditionNotFoundReports")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/not-found-reports/rare-books/{rareBookId:guid}/found",
                    async (
                        Guid rareBookId,
                        NotFoundReportFoundRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new MarkNotFoundTargetFoundCommand(
                                RareTarget(rareBookId), request.Note, userId),
                            cancellationToken);
                        return result.Match(
                            closure => Results.Ok(ToResponse(closure)),
                            error => error.Result());
                    })
                .WithName("MarkRareBookNotFoundReportsFound")
                .RequireAuthorization("RareBooks");

            endpoints.MapPost(
                    "/books/admin/not-found-reports/rare-books/{rareBookId:guid}/withdrawal",
                    async (
                        Guid rareBookId,
                        RareBookNotFoundReportWithdrawalRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        if (!TryParseReason(request.Reason, out var reason))
                        {
                            return Results.BadRequest(new { error = "The withdrawal reason is not recognised." });
                        }

                        var result = await mediator.Send(
                            new WithdrawNotFoundTargetCommand(
                                RareTarget(rareBookId), 0, reason, request.Note, userId),
                            cancellationToken);
                        return result.Match(
                            closure => Results.Ok(ToResponse(closure)),
                            error => error.Result());
                    })
                .WithName("WithdrawRareBookNotFoundReports")
                .RequireAuthorization("RareBooks");

            endpoints.MapPost(
                    "/books/admin/not-found-reports/rare-books/{rareBookId:guid}/dismissal",
                    async (
                        Guid rareBookId,
                        NotFoundReportDismissalRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new DismissNotFoundTargetCommand(RareTarget(rareBookId), request.Note, userId),
                            cancellationToken);
                        return result.Match(
                            closure => Results.Ok(ToResponse(closure)),
                            error => error.Result());
                    })
                .WithName("DismissRareBookNotFoundReports")
                .RequireAuthorization("RareBooks");
        });
    }

    private static NotFoundReportTargetRef RareTarget(Guid rareBookId) =>
        new("rare", rareBookId.ToString("D"));

    private static bool IsRareBooksOnly(ClaimsPrincipal principal) =>
        principal.IsInRole("LivresRares") &&
        !principal.IsInRole("Administration") &&
        !principal.IsInRole("Admin");

    private static bool TryGetUserId(ClaimsPrincipal principal, out UserId userId)
    {
        var externalId = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        if (Guid.TryParse(externalId, out var value) && value != Guid.Empty)
        {
            userId = UserId.Create(value);
            return true;
        }

        userId = null!;
        return false;
    }

    private static bool TryParseReason(string? value, out NotFoundWithdrawalReason reason) =>
        Enum.TryParse(value, ignoreCase: true, out reason) && Enum.IsDefined(reason);

    private static NotFoundReportQueueResponse ToResponse(NotFoundReportQueueResult result) =>
        new(
            result.GeneratedAt,
            result.OpenTargetCount,
            result.OpenReportCount,
            result.OverdueTargetCount,
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.Items.Select(item => new NotFoundReportTargetResponse(
                item.Kind,
                item.Isbn13,
                item.RareBookId,
                item.Title,
                item.Authors,
                item.Publisher,
                item.PublicationYear,
                item.CoverUrl,
                item.Genre,
                item.QuantityAvailable,
                item.ReportCount,
                item.MemberCount,
                item.FirstReportedAt,
                item.LastReportedAt,
                item.Overdue,
                item.Comments.Select(ToResponse).ToArray())).ToArray());

    private static ClosedNotFoundReportsResponse ToResponse(ClosedNotFoundReportsResult result) =>
        new(
            result.GeneratedAt,
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.Items.Select(item => new ClosedNotFoundReportResponse(
                item.ClosedAt,
                item.Kind,
                item.Isbn13,
                item.RareBookId,
                item.Title,
                item.Outcome,
                item.ReportCount,
                item.WithdrawnQuantity,
                item.ClosedByName,
                item.Note)).ToArray());

    private static NotFoundReportSummaryResponse ToResponse(NotFoundReportSummaryResult result) =>
        new(
            result.OpenTargetCount,
            result.OverdueTargetCount,
            result.OpenReportCount,
            result.FirstReportedAt,
            result.LatestComment is null ? null : ToResponse(result.LatestComment));

    private static NotFoundReportCommentResponse ToResponse(NotFoundReportCommentResult result) =>
        new(result.Text, result.Location, result.ReportedAt);

    private static NotFoundClosureResponse ToResponse(NotFoundClosureResult result) =>
        new(result.ClosedReportCount, result.WithdrawnQuantity, result.QuantityAvailable, result.MovementId);
}
