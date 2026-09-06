using System.Security.Claims;
using MediatR;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.AccountAdministration;
using Vole_Papillon_Damour.Contracts.Accounts.Requests;
using Vole_Papillon_Damour.Contracts.Accounts.Responses;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class AccountAdministrationController
{
    public static IApplicationBuilder UseAccountAdministrationController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(
                    "/accounts/admin",
                    async (
                        string? search,
                        int? page,
                        int? pageSize,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var result = await mediator.Send(
                            new GetAdminAccountsQuery(search, page ?? 1, pageSize ?? 50),
                            cancellationToken);
                        return result.Match(
                            accounts => Results.Ok(ToResponse(accounts)),
                            error => error.Result());
                    })
                .WithName("GetAdminAccounts")
                .RequireAuthorization("Administration");

            endpoints.MapPost(
                    "/accounts/admin",
                    async (
                        CreateAdminAccountRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetExternalId(principal, out _))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new CreateAdminAccountCommand(
                                request.Email,
                                request.DisplayName,
                                request.TemporaryPassword,
                                request.Roles),
                            cancellationToken);
                        return result.Match(
                            account => Results.Created(
                                $"/accounts/admin/{Uri.EscapeDataString(account.ExternalId)}",
                                ToResponse(account)),
                            error => error.Result());
                    })
                .WithName("CreateAdminAccount")
                .RequireAuthorization("Administration");

            endpoints.MapPut(
                    "/accounts/admin/{externalId}/roles",
                    async (
                        string externalId,
                        UpdateAdminAccountRolesRequest request,
                        ClaimsPrincipal principal,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        if (!TryGetExternalId(principal, out var requesterExternalId))
                        {
                            return Results.Unauthorized();
                        }

                        var result = await mediator.Send(
                            new UpdateAdminAccountRolesCommand(
                                externalId,
                                requesterExternalId,
                                request.Roles),
                            cancellationToken);
                        return result.Match(
                            account => Results.Ok(ToResponse(account)),
                            error => error.Result());
                    })
                .WithName("UpdateAdminAccountRoles")
                .RequireAuthorization("Administration");
        });
    }

    private static AdminAccountPageResponse ToResponse(AdminAccountPageResult result) =>
        new(
            result.GeneratedAt,
            result.Accounts.Select(ToResponse).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);

    private static AdminAccountResponse ToResponse(AdminAccountResult result) =>
        new(
            result.ExternalId,
            result.Email,
            result.DisplayName,
            result.AccountEnabled,
            result.CreatedAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(result.CreatedAt.Value, DateTimeKind.Utc)),
            result.Roles);

    private static bool TryGetExternalId(ClaimsPrincipal principal, out string externalId)
    {
        externalId = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(
                "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? string.Empty;
        return Guid.TryParse(externalId, out _);
    }
}
