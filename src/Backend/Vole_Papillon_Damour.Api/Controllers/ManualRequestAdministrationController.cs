using System.Security.Claims;
using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.AddRequest;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.Cancel;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.CreateRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.DeleteRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.MarkGiven;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.PickFromStock;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.Refuse;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.UpdateRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequesters;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetStock;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetSummary;
using Vole_Papillon_Damour.Contracts.ManualRequests;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class ManualRequestAdministrationController
{
    public static IApplicationBuilder UseManualRequestAdministrationController(
        this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/books/admin/manual-requests",
                    async (
                        string? tab,
                        string? q,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryParseEnum(tab ?? "Found", out ManualRequestTab parsedTab))
                        {
                            return Error.Validation(
                                "ManualRequest.InvalidTab",
                                "The manual request tab is not supported.").Result();
                        }

                        var result = await mediator.Send(
                            new GetManualRequestQueueQuery(parsedTab, q, page ?? 1, pageSize ?? 50),
                            cancellationToken);
                        return result.Match(
                            queue => Results.Ok(ToResponse(queue)),
                            error => error.Result());
                    })
                .WithName("GetManualRequestQueue")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/manual-requests/summary",
                    async (IMediator mediator, CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetManualRequestSummaryQuery(),
                            cancellationToken);
                        return result.Match(
                            summary => Results.Ok(new ManualRequestSummaryResponse(
                                summary.FoundCount,
                                summary.FoundOverdueCount)),
                            error => error.Result());
                    })
                .WithName("GetManualRequestSummary")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/manual-requests/stock",
                    async (
                        string? scope,
                        string? workId,
                        string? isbn13,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryParseEnum(scope, out ManualBookRequestScope parsedScope))
                        {
                            return Error.Validation(
                                "ManualRequest.InvalidTarget",
                                "The request target must identify a work or an ISBN-13 edition.").Result();
                        }

                        var result = await mediator.Send(
                            new GetManualRequestStockQuery(parsedScope, workId, isbn13),
                            cancellationToken);
                        return result.Match(
                            stock => Results.Ok(new ManualRequestStockResponse(
                                stock.QuantityAvailable,
                                stock.QuantityAnnounced,
                                stock.NextFairStart)),
                            error => error.Result());
                    })
                .WithName("GetManualRequestStock")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/manual-requests/{requestId:guid}/pick",
                    async (
                        Guid requestId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        var result = await mediator.Send(
                            new PickManualRequestFromStockCommand(requestId, userId),
                            cancellationToken);
                        return result.Match(_ => Results.NoContent(), error => error.Result());
                    })
                .WithName("PickManualRequestFromStock")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/manual-requests/{requestId:guid}/given",
                    async (
                        Guid requestId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        var result = await mediator.Send(
                            new MarkManualRequestGivenCommand(requestId, userId),
                            cancellationToken);
                        return result.Match(_ => Results.NoContent(), error => error.Result());
                    })
                .WithName("MarkManualRequestGiven")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/manual-requests/{requestId:guid}/refuse",
                    async (
                        Guid requestId,
                        RefuseManualRequestRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        [FromServices] BookAlertDeliveryService alertDelivery,
                        [FromServices] ILoggerFactory loggerFactory,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        var result = await mediator.Send(
                            new RefuseManualRequestCommand(requestId, request.KeepSearching, userId),
                            cancellationToken);
                        return await result.Match<Task<IResult>>(
                            async release =>
                            {
                                if (release.NotifiedMemberCount > 0)
                                {
                                    await TryDeliverAlertsAsync(
                                        alertDelivery,
                                        loggerFactory.CreateLogger("ManualRequestAdministration"),
                                        cancellationToken);
                                }

                                return Results.Ok(new ManualRequestReleaseResponse(release.NotifiedMemberCount));
                            },
                            error => Task.FromResult(error.Result()));
                    })
                .WithName("RefuseManualRequest")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/manual-requests/{requestId:guid}/cancel",
                    async (
                        Guid requestId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        [FromServices] BookAlertDeliveryService alertDelivery,
                        [FromServices] ILoggerFactory loggerFactory,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        var result = await mediator.Send(
                            new CancelManualRequestCommand(requestId, userId),
                            cancellationToken);
                        return await result.Match<Task<IResult>>(
                            async release =>
                            {
                                if (release.NotifiedMemberCount > 0)
                                {
                                    await TryDeliverAlertsAsync(
                                        alertDelivery,
                                        loggerFactory.CreateLogger("ManualRequestAdministration"),
                                        cancellationToken);
                                }

                                return Results.Ok(new ManualRequestReleaseResponse(release.NotifiedMemberCount));
                            },
                            error => Task.FromResult(error.Result()));
                    })
                .WithName("CancelManualRequest")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/manual-requesters",
                    async (
                        string? q,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetManualRequestersQuery(q, page ?? 1, pageSize ?? 50),
                            cancellationToken);
                        return result.Match(
                            requesters => Results.Ok(ToResponse(requesters)),
                            error => error.Result());
                    })
                .WithName("GetManualRequesters")
                .RequireAuthorization("Administration");

            endpoints.MapGet(
                    "/books/admin/manual-requesters/{requesterId:guid}",
                    async (
                        Guid requesterId,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetManualRequesterQuery(requesterId),
                            cancellationToken);
                        return result.Match(
                            detail => Results.Ok(ToResponse(detail)),
                            error => error.Result());
                    })
                .WithName("GetManualRequester")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/manual-requesters",
                    async (
                        CreateManualRequesterRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        var result = await mediator.Send(
                            new CreateManualRequesterCommand(
                                request.LastName,
                                request.FirstName,
                                request.Phone,
                                request.ConsentGiven,
                                userId),
                            cancellationToken);
                        return result.Match(
                            requesterId => Results.Json(
                                new ManualRequesterCreatedResponse(requesterId),
                                statusCode: StatusCodes.Status201Created),
                            error => error.Result());
                    })
                .WithName("CreateManualRequester")
                .RequireAuthorization("Administration");

            endpoints.MapPut(
                    "/books/admin/manual-requesters/{requesterId:guid}",
                    async (
                        Guid requesterId,
                        UpdateManualRequesterRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        var result = await mediator.Send(
                            new UpdateManualRequesterCommand(
                                requesterId,
                                request.LastName,
                                request.FirstName,
                                request.Phone,
                                userId),
                            cancellationToken);
                        return result.Match(_ => Results.NoContent(), error => error.Result());
                    })
                .WithName("UpdateManualRequester")
                .RequireAuthorization("Administration");

            endpoints.MapDelete(
                    "/books/admin/manual-requesters/{requesterId:guid}",
                    async (
                        Guid requesterId,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        [FromServices] BookAlertDeliveryService alertDelivery,
                        [FromServices] ILoggerFactory loggerFactory,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        var result = await mediator.Send(
                            new DeleteManualRequesterCommand(requesterId, userId),
                            cancellationToken);
                        return await result.Match<Task<IResult>>(
                            async deletion =>
                            {
                                if (deletion.NotifiedMemberCount > 0)
                                {
                                    await TryDeliverAlertsAsync(
                                        alertDelivery,
                                        loggerFactory.CreateLogger("ManualRequestAdministration"),
                                        cancellationToken);
                                }

                                return Results.Ok(new ManualRequesterDeletionResponse(
                                    deletion.CancelledRequestCount,
                                    deletion.NotifiedMemberCount));
                            },
                            error => Task.FromResult(error.Result()));
                    })
                .WithName("DeleteManualRequester")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/books/admin/manual-requesters/{requesterId:guid}/requests",
                    async (
                        Guid requesterId,
                        AddManualBookRequestRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                        if (!TryParseEnum(request.Scope, out ManualBookRequestScope scope))
                        {
                            return Error.Validation(
                                "ManualRequest.InvalidTarget",
                                "The request target must identify a work or an ISBN-13 edition.").Result();
                        }

                        var result = await mediator.Send(
                            new AddManualBookRequestCommand(
                                requesterId,
                                scope,
                                request.WorkId,
                                request.Isbn13,
                                request.Title,
                                request.Authors,
                                request.Publisher,
                                request.PublicationYear,
                                request.PickFromStock,
                                userId),
                            cancellationToken);
                        return result.Match(
                            created => Results.Json(
                                new ManualBookRequestCreatedResponse(created.RequestId, created.Status.ToString()),
                                statusCode: StatusCodes.Status201Created),
                            error => error.Result());
                    })
                .WithName("AddManualBookRequest")
                .RequireAuthorization("Administration");
        });
    }

    private static async Task TryDeliverAlertsAsync(
        BookAlertDeliveryService alertDelivery,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await alertDelivery.ProcessPendingAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Immediate manual request alert delivery failed; the scheduled sweep will retry.");
        }
    }

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

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    private static ManualRequestQueueResponse ToResponse(ManualRequestQueueResult result) =>
        new(
            result.Items.Select(ToResponse).ToArray(),
            result.TotalCount,
            result.FoundCount,
            result.SearchingCount,
            result.ClosedCount,
            result.FoundOverdueCount,
            result.Page,
            result.PageSize);

    private static ManualRequestQueueItemResponse ToResponse(ManualRequestQueueItem item) =>
        new(
            item.RequestId,
            item.RequesterId,
            item.RequesterLastName,
            item.RequesterFirstName,
            item.RequesterPhone,
            item.RequesterErased,
            item.Scope.ToString(),
            item.WorkId,
            item.Isbn13,
            item.Title,
            item.Authors,
            item.Publisher,
            item.PublicationYear,
            item.RequestedAt,
            item.Status.ToString(),
            item.FoundOrigin?.ToString(),
            item.FoundIsbn13,
            item.FoundPublisher,
            item.FoundPublicationYear,
            item.FoundAt,
            item.FoundByName,
            item.QuantityAvailable,
            item.QuantityAnnounced,
            item.WatcherCount,
            item.ReleaseMode?.ToString(),
            item.ReleaseFairStart,
            item.ClosedAt,
            item.ClosedByName);

    private static ManualRequesterDetailResponse ToResponse(ManualRequesterDetailResult result) =>
        new(
            result.RequesterId,
            result.LastName,
            result.FirstName,
            result.Phone,
            result.Erased,
            result.ConsentRecordedAt,
            result.ConsentRecordedByName,
            result.Requests.Select(ToResponse).ToArray());

    private static ManualRequesterPageResponse ToResponse(ManualRequesterPageResult result) =>
        new(
            result.Requesters.Select(requester => new ManualRequesterSummaryResponse(
                requester.RequesterId,
                requester.LastName,
                requester.FirstName,
                requester.Phone,
                requester.RequestCount,
                requester.SearchingCount,
                requester.FoundCount)).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
}
