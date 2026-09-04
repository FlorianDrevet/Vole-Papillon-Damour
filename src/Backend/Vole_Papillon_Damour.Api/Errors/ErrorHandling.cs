using Microsoft.AspNetCore.Diagnostics;

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
