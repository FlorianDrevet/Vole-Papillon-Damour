using System.Security.Claims;
using MediatR;
using Vole_Papillon_Damour.Api.Common.RateLimiting;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.CheckoutPassages.Commands.AssociateCheckoutPassage;
using Vole_Papillon_Damour.Application.CheckoutPassages.Commands.DissociateCheckoutPassage;
using Vole_Papillon_Damour.Application.CheckoutPassages.Queries.LookupCheckoutPassage;
using Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;
using Vole_Papillon_Damour.Contracts.CheckoutPassages;
using Vole_Papillon_Damour.Contracts.MemberCards;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class CheckoutPassageController
{
    public static IApplicationBuilder UseCheckoutPassageController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPut(
                    "/scan/passages/{checkoutPassageId:guid}/member",
                    async (
                        Guid checkoutPassageId,
                        AssociateCheckoutPassageRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (string.IsNullOrWhiteSpace(request.Credential))
                        {
                            return Results.BadRequest();
                        }

                        if (!TryGetUserId(principal, out var volunteerId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new AssociateCheckoutPassageCommand(
                                checkoutPassageId,
                                request.Credential,
                                request.OccurredAt.UtcDateTime,
                                volunteerId),
                            cancellationToken);
                        return result.Match(
                            association => Results.Ok(new AssociateCheckoutPassageResponse(
                                association.CheckoutPassageId,
                                association.Status,
                                association.DisplayLabel,
                                association.AlreadyProcessed)),
                            error => error.Result());
                    })
                .WithName("AssociateCheckoutPassageMember")
                .RequireAuthorization("Caisse");

            endpoints.MapPost(
                    "/scan/member-cards/resolve",
                    async (
                        ResolveMemberCardRequest request,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (string.IsNullOrWhiteSpace(request.Credential))
                        {
                            return Results.BadRequest();
                        }

                        var result = await mediator.Send(
                            new ResolveMemberCardQuery(request.Credential),
                            cancellationToken);
                        return result.Match(
                            member => Results.Ok(new ResolveMemberCardResponse(member.DisplayLabel)),
                            error => error.Result());
                    })
                .WithName("ResolveMemberCardForCheckout")
                .RequireAuthorization("Caisse")
                .RequireRateLimiting(RateLimitingPolicies.MemberCardResolve);

            endpoints.MapGet(
                    "/administration/checkout-passages/lookup",
                    async (
                        string reference,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new LookupCheckoutPassageQuery(reference),
                            cancellationToken);
                        return result.Match(
                            passage => Results.Ok(new CheckoutPassageLookupResponse(
                                passage.Id,
                                new DateTimeOffset(DateTime.SpecifyKind(passage.OccurredAt, DateTimeKind.Utc)),
                                passage.LineCount,
                                passage.DisplayLabel)),
                            error => error.Result());
                    })
                .WithName("LookupCheckoutPassageForAdministration")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/administration/checkout-passages/{id:guid}/dissociate",
                    async (
                        Guid id,
                        DissociateCheckoutPassageRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetUserId(principal, out var administratorId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new DissociateCheckoutPassageCommand(id, administratorId, request.Reason),
                            cancellationToken);
                        return result.Match(
                            _ => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("DissociateCheckoutPassage")
                .RequireAuthorization("Administration");
        });
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
}
