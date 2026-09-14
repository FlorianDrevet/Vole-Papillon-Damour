using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Vole_Papillon_Damour.Application.Common.Persistence;

/// <summary>
/// Server-side rowversion comparisons, which LINQ cannot express on byte arrays.
/// </summary>
public static class RowVersionQueries
{
    private const string SqlServerProviderName = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>
    /// Translated to a binary <c>&gt;</c>: SQL Server and SQLite both order binary
    /// values byte by byte, which matches the big-endian rowversion encoding.
    /// </summary>
    public static bool IsGreaterThan(byte[] left, byte[] right)
    {
        throw new InvalidOperationException(
            $"{nameof(IsGreaterThan)} can only be evaluated inside a database query.");
    }

    public static void Register(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder
            .HasDbFunction(typeof(RowVersionQueries).GetMethod(nameof(IsGreaterThan))!)
            .HasTranslation(arguments => new SqlBinaryExpression(
                ExpressionType.GreaterThan,
                arguments[0],
                arguments[1],
                typeof(bool),
                new BoolTypeMapping("bit")));
    }

    /// <summary>
    /// Returns <c>MIN_ACTIVE_ROWVERSION()</c> on SQL Server, or <c>null</c> on providers
    /// without server-assigned rowversions. Every row below that value is committed,
    /// so a read bounded by it cannot skip a write that is still in flight.
    /// </summary>
    public static async Task<byte[]?> GetMinActiveRowVersionAsync(
        DatabaseFacade database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!string.Equals(database.ProviderName, SqlServerProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        return await database
            .SqlQueryRaw<byte[]>("SELECT MIN_ACTIVE_ROWVERSION() AS [Value]")
            .SingleAsync(cancellationToken);
    }
}
