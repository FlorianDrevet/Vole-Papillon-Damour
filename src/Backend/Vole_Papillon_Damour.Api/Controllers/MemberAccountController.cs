using System.Security.Claims;
using MediatR;
using Vole_Papillon_Damour.Api.Common;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.MergeSelection;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.RemoveSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.SetSelectionItemStatus;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Application.MemberSelection.Queries.GetMySelection;
using Vole_Papillon_Damour.Contracts.MemberSelection;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class MemberAccountController
{
    public static IApplicationBuilder UseMemberAccountController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
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
                    async (
                        Guid id,
                        SetSelectionItemStatusRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!MemberIdentityClaims.TryGetMemberIdentity(principal, out var identity))
                        {
                            return Results.Unauthorized();
                        }

                        if (!Enum.TryParse<MemberSelectionStatus>(
                                request.Status,
                                ignoreCase: true,
                                out var status) ||
                            !Enum.IsDefined(status))
                        {
                            return DomainErrors.MemberSelection.InvalidStatus().Result();
                        }

                        var result = await mediator.Send(
                            new SetSelectionItemStatusCommand(
                                identity.ExternalId,
                                identity.Email,
                                identity.FirstName,
                                identity.LastName,
                                id,
                                status),
                            cancellationToken);
                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
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
                item.PurchasedAt)).ToArray());
}

