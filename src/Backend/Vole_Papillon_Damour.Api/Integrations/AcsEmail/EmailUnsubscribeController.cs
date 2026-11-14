using ErrorOr;
using MediatR;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RevokeFairDigestConsent;
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
                        if (!tokenService.TryValidate(token, out var memberId, out var purpose))
                        {
                            return Results.BadRequest();
                        }

                        switch (purpose)
                        {
                            case UnsubscribePurpose.BookAlerts:
                                return ToResponse(await mediator.Send(
                                    new UnsubscribeMemberAlertsCommand(memberId),
                                    cancellationToken));
                            case UnsubscribePurpose.FairDigest:
                                return ToResponse(await mediator.Send(
                                    new RevokeFairDigestConsentCommand(memberId),
                                    cancellationToken));
                            default:
                                return Results.BadRequest();
                        }
                    })
                .WithName("UnsubscribeFromBookAlerts")
                .AllowAnonymous();

            endpoints.MapGet(
                    Route,
                    (
                        string? token,
                        IUnsubscribeTokenService tokenService,
                        IOptions<UnsubscribeTokenOptions> options) =>
                    {
                        if (tokenService.TryValidate(token ?? string.Empty, out _, out var purpose) &&
                            purpose == UnsubscribePurpose.FairDigest)
                        {
                            return Results.Redirect(BuildFairDigestLandingUrl(options.Value.AccountUrl), permanent: false);
                        }

                        return Results.Redirect(options.Value.AccountUrl, permanent: false);
                    })
                .WithName("UnsubscribeFromBookAlertsLanding")
                .AllowAnonymous();
        });
    }

    private static IResult ToResponse<T>(ErrorOr<T> result) => result.IsError
        ? result.Errors.First().Result()
        : Results.NoContent();

    private static string BuildFairDigestLandingUrl(string accountUrl)
    {
        if (!Uri.TryCreate(accountUrl, UriKind.Absolute, out var accountUri) ||
            accountUri.Scheme is not ("http" or "https"))
        {
            return accountUrl;
        }

        return new UriBuilder(accountUri)
        {
            Path = "/desinscription",
            Query = "purpose=fairDigest",
            Fragment = string.Empty
        }.Uri.AbsoluteUri;
    }
}
