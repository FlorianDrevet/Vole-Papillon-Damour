using MediatR;
using Vole_Papillon_Damour.Api.Common.RateLimiting;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;
using Vole_Papillon_Damour.Contracts.MemberCards;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class CheckoutPassageController
{
    public static IApplicationBuilder UseCheckoutPassageController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
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
        });
    }
}
