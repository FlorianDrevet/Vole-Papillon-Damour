using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Vole_Papillon_Damour.Worker;

public static class WorkerLoggingConfiguration
{
    public static void Configure(ILoggingBuilder logging, bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(logging);

        // SQL statements can contain parameter values, and HttpClient request logs
        // duplicate the dependency spans already exported to Application Insights.
        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

        if (isProduction)
        {
            // Production console output is billable in the Container Apps workspace.
            // Keep application diagnostics and lifecycle messages, plus warnings and
            // errors; Azure SDK and Durable Task information logs are verbose duplicates.
            logging.AddFilter<ConsoleLoggerProvider>((category, level) =>
                ShouldLogToConsole(category, level));
        }
    }

    public static bool ShouldLogToConsole(string? category, LogLevel level)
    {
        if (level == LogLevel.None)
        {
            return false;
        }

        if (level >= LogLevel.Warning)
        {
            return true;
        }

        if (IsVerboseCategory(category))
        {
            return false;
        }

        return level == LogLevel.Information && IsDiagnosticSummaryCategory(category);
    }

    private static bool IsVerboseCategory(string? category) =>
        category?.StartsWith("Microsoft.EntityFrameworkCore.Database.Command", StringComparison.Ordinal) == true
        || category?.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal) == true;

    private static bool IsDiagnosticSummaryCategory(string? category) =>
        category == "Microsoft.Hosting.Lifetime"
        || category?.StartsWith("Microsoft.Hosting.Lifetime.", StringComparison.Ordinal) == true
        || category?.StartsWith("Vole_Papillon_Damour.", StringComparison.Ordinal) == true;
}
