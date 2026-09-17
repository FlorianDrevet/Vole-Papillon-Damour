using MediatR;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.UnsubscribeMemberAlerts;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Api.Integrations.AcsEmail;

/// <summary>
/// RFC 8058 one-click unsubscribe. Mailbox providers POST here without any
/// credential, so the caller is identified solely by the HMAC-signed token in
/// the query string. A GET carries no such promise and never changes state:
/// mail scanners and link prefetchers follow links, and a GET that
/// unsubscribed would let them silently opt members out.
/// </summary>
public static class EmailUnsubscribeController
{
    public const string Route = "/integrations/email/unsubscribe";

    public static IApplicationBuilder UseEmailUnsubscribeController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(
                    Route,
                    async (
                        HttpContext httpContext,
                        IUnsubscribeTokenService tokenService,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var token = httpContext.Request.Query["token"].ToString();
                        if (!tokenService.TryValidate(token, out var memberId))
                        {
                            return Results.BadRequest();
                        }

                        var result = await mediator.Send(
                            new UnsubscribeMemberAlertsCommand(memberId),
                            cancellationToken);

                        return result.IsError
                            ? result.Errors.First().Result()
                            : Results.NoContent();
                    })
                .WithName("UnsubscribeFromBookAlerts")
                .AllowAnonymous();

            endpoints.MapGet(
                    Route,
                    (IOptions<UnsubscribeTokenOptions> options) =>
                        Results.Redirect(options.Value.AccountUrl, permanent: false))
                .WithName("UnsubscribeFromBookAlertsLanding")
                .AllowAnonymous();
        });
    }
}
