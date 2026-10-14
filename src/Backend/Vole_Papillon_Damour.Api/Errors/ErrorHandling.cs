using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Vole_Papillon_Damour.Api.Errors;

public static class ErrorHandling
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseExceptionHandler(exceptionHandlerApp 
            => exceptionHandlerApp.Run(async context 
                    =>
                {
                    var (statusCode, message) = context.Features.Get<IExceptionHandlerFeature>()?.Error switch
                    {
                        // External bibliographic providers are dependencies of the
                        // public metadata probe. A transient outage is a 503, not
                        // an application failure and not a reason to expose details.
                        HttpRequestException => (
                            StatusCodes.Status503ServiceUnavailable,
                            "The bibliographic metadata providers are temporarily unavailable."),
                        // Two live tableau actions on the same event at the same time:
                        // the second one is rejected and can be retried.
                        DbUpdateConcurrencyException => (
                            StatusCodes.Status409Conflict,
                            "The resource was modified at the same time. Please retry."),
                        _ => (StatusCodes.Status500InternalServerError, "An error occurred.")
                    };
                    await Results.Problem(
                            statusCode: statusCode,
                            detail: message
                        )
                        .ExecuteAsync(context);
                }
            )
        );
    }
}
