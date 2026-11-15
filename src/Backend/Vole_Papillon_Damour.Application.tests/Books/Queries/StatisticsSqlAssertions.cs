using FluentAssertions;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

internal static class StatisticsSqlAssertions
{
    public static void AssertBookAndMovementQueriesAreNarrow(
        SqlCaptureInterceptor sqlCapture,
        bool expectBookQuery = true)
    {
        var bookQueries = sqlCapture.Commands
            .Where(command => command.Contains("FROM \"Books\"", StringComparison.Ordinal))
            .ToArray();
        if (expectBookQuery)
        {
            bookQueries.Should().NotBeEmpty();
        }

        bookQueries.Should().OnlyContain(command =>
            command.Contains("\"Id\" IN ", StringComparison.Ordinal) ||
            command.Contains("\"Id\" = ", StringComparison.Ordinal));
        bookQueries.Should().OnlyContain(command =>
            !command.Contains("\"RawPayload\"", StringComparison.Ordinal) &&
            !command.Contains("\"Summary\"", StringComparison.Ordinal));

        var movementQueries = sqlCapture.Commands
            .Where(command => command.Contains("FROM \"BookMovements\"", StringComparison.Ordinal))
            .ToArray();
        movementQueries.Should().NotBeEmpty();
        movementQueries.Should().OnlyContain(command =>
            !command.Contains("\"ReceivedAt\"", StringComparison.Ordinal) &&
            !command.Contains("\"ClockSuspect\"", StringComparison.Ordinal) &&
            !command.Contains("\"Note\"", StringComparison.Ordinal) &&
            !command.Contains("\"ClientGestureId\"", StringComparison.Ordinal) &&
            !command.Contains("\"CheckoutPassageId\"", StringComparison.Ordinal));
    }
}
