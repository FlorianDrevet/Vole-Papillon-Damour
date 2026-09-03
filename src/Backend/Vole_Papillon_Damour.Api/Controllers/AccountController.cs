using System.Security.Claims;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class AccountController
{
    public static IApplicationBuilder UseAccountController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapDelete(
                    "/catalog/me",
                    async (
                        ClaimsPrincipal principal,
                        IAccountDeletionService accountDeletionService,
                        CancellationToken cancellationToken) =>
                    {
                        var externalId = principal.FindFirst("oid")?.Value
                            ?? principal.FindFirst(
                                "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

                        if (!Guid.TryParse(externalId, out _))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await accountDeletionService.RequestAsync(
                            externalId,
                            cancellationToken);

                        return result.IsCompleted
                            ? Results.NoContent()
                            : Results.Accepted();
                    })
                .WithName("DeleteMyAccount")
                .RequireAuthorization();
        });
    }
}
