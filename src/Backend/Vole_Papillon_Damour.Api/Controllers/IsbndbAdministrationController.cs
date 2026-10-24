using System.Security.Claims;
using MediatR;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Isbndb.Commands.RecheckIsbndbBook;
using Vole_Papillon_Damour.Application.Isbndb.Commands.SetIsbndbCompletion;
using Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbAbsentBooks;
using Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbStatus;
using Vole_Papillon_Damour.Contracts.Isbndb;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class IsbndbAdministrationController
{
    public static IApplicationBuilder UseIsbndbAdministrationController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/books/admin/isbndb/status", async (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetIsbndbStatusQuery(), cancellationToken);
                return Results.Ok(new IsbndbStatusResponse(
                    result.DailyLimit,
                    result.DailyRemaining,
                    result.ObservedAt,
                    result.ResetsAt,
                    result.Status,
                    result.ExhaustedUntil,
                    result.UsageToday.Select(item => new IsbndbUsageTodayResponse(item.Usage, item.Credits)).ToArray(),
                    new IsbndbPendingCatchUpResponse(
                        result.PendingCatchUp.Total,
                        result.PendingCatchUp.InStock,
                        result.PendingCatchUp.BeforeMigration),
                    result.AbsentToComplete,
                    result.LastRun is null ? null : new IsbndbLastRunResponse(
                        result.LastRun.StartedAt,
                        result.LastRun.EndedAt,
                        result.LastRun.Filled,
                        result.LastRun.Absent,
                        result.LastRun.Remaining,
                        result.LastRun.StopReason),
                    result.UnmatchedSubjects.Select(item => new IsbndbUnmatchedSubjectResponse(item.Subject, item.Count)).ToArray()));
            }).WithName("GetIsbndbStatus").RequireAuthorization("Administration");

            endpoints.MapGet("/books/admin/books/isbndb-absent", async (
                string? status,
                string? search,
                int? page,
                int? pageSize,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new GetIsbndbAbsentBooksQuery(status, search, page ?? 1, pageSize ?? 50), cancellationToken);
                return result.Match(
                    value => Results.Ok(new IsbndbAbsentBooksPageResponse(
                        value.Items.Select(item => new IsbndbAbsentBookResponse(
                            item.Isbn13, item.Title, item.Authors, item.CoverUrl, item.FallbackSource,
                            item.QuantityAvailable, item.MissingFields, item.Summary, item.Genre, item.Publisher,
                            item.PublicationYear, item.Pages, item.Edition, item.Genres, item.Subjects,
                            item.FirstSeenAt, item.IsbndbCheckedAt, item.IsbndbCompletedAt)).ToArray(),
                        value.TotalCount, value.Page, value.PageSize)),
                    error => error.Result());
            }).WithName("GetIsbndbAbsentBooks").RequireAuthorization("Administration");

            endpoints.MapPost("/books/admin/books/{isbn13}/isbndb-completion", async (
                string isbn13,
                HttpRequest httpRequest,
                ClaimsPrincipal principal,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var administrator)) return Results.Unauthorized();
                var request = await httpRequest.ReadFromJsonAsync<SetIsbndbCompletionRequest>(cancellationToken);
                if (request is null) return Results.BadRequest(new { error = "Completion state is required." });
                var result = await mediator.Send(
                    new SetIsbndbCompletionCommand(isbn13, request.Completed, administrator), cancellationToken);
                return result.Match(
                    value => Results.Ok(new IsbndbCompletionResponse(
                        value.Isbn13, value.IsCompleted, value.CompletedAt, value.CompletedBy, value.Changed)),
                    error => error.Result());
            }).WithName("SetIsbndbCompletion").RequireAuthorization("Administration");

            endpoints.MapPost("/books/admin/books/{isbn13}/isbndb-recheck", async (
                string isbn13,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new RecheckIsbndbBookCommand(isbn13), cancellationToken);
                return result.Match<IResult>(
                    value => value.Outcome switch
                    {
                        "QuotaExhausted" => Results.Conflict(new IsbndbRecheckResponse(value.Isbn13, value.Outcome, value.ResetsAt)),
                        "Unavailable" => Results.Json(new IsbndbRecheckResponse(value.Isbn13, value.Outcome), statusCode: StatusCodes.Status503ServiceUnavailable),
                        _ => Results.Ok(new IsbndbRecheckResponse(value.Isbn13, value.Outcome))
                    },
                    error => error.Result());
            }).WithName("RecheckIsbndbBook").RequireAuthorization("Administration");
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
