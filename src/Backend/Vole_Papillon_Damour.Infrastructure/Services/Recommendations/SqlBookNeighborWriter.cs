using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public sealed class SqlBookNeighborWriter(ProjectDbContext dbContext) : IBookNeighborWriter
{
    private const int BulkBatchSize = 10_000;

    public async Task<Guid> WriteIncrementalGenerationAsync(
        Guid generationId,
        IReadOnlyCollection<string> replacedIsbns,
        IReadOnlyCollection<BookNeighborRow> replacementRows,
        int bookCount,
        DateTime computedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacedIsbns);
        ArgumentNullException.ThrowIfNull(replacementRows);
        if (!dbContext.Database.IsSqlServer())
        {
            throw new InvalidOperationException("Book neighbor generations can only be written to SQL Server.");
        }

        var activeGenerationId = generationId;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            activeGenerationId = await LockCurrentGenerationAsync(connection, sqlTransaction, cancellationToken)
                                 ?? generationId;
            var replaced = replacedIsbns.Distinct(StringComparer.Ordinal).ToArray();
            var replacementArray = replacementRows as BookNeighborRow[] ?? replacementRows.ToArray();

            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TABLE #ReplacedIsbns ([Isbn13] char(13) NOT NULL PRIMARY KEY);",
                cancellationToken);
            if (replaced.Length > 0)
            {
                var keyTable = new DataTable();
                keyTable.Columns.Add("Isbn13", typeof(string));
                foreach (var isbn13 in replaced)
                {
                    keyTable.Rows.Add(isbn13);
                }

                using var keyCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, sqlTransaction)
                {
                    DestinationTableName = "#ReplacedIsbns",
                    BatchSize = BulkBatchSize,
                };
                keyCopy.ColumnMappings.Add("Isbn13", "Isbn13");
                await keyCopy.WriteToServerAsync(keyTable, cancellationToken);
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE current
                  FROM [dbo].[BookNeighbors] AS current
                  INNER JOIN #ReplacedIsbns AS replaced ON replaced.[Isbn13] = current.[Isbn13]
                 WHERE current.[GenerationId] = {activeGenerationId}
                """, cancellationToken);

            for (var offset = 0; offset < replacementArray.Length; offset += BulkBatchSize)
            {
                var count = Math.Min(BulkBatchSize, replacementArray.Length - offset);
                var table = CreateTable(activeGenerationId, replacementArray.AsSpan(offset, count));
                using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, sqlTransaction)
                {
                    DestinationTableName = "[dbo].[BookNeighbors]",
                    BatchSize = BulkBatchSize,
                };
                MapNeighborColumns(bulkCopy);
                await bulkCopy.WriteToServerAsync(table, cancellationToken);
            }

            var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [dbo].[RecommendationGenerations]
                   SET [CurrentGenerationId] = {activeGenerationId},
                       [BookCount] = {bookCount},
                       [ComputedAt] = {computedAt}
                 WHERE [Id] = 1
                """, cancellationToken);
            if (updated != 1)
            {
                throw new InvalidOperationException("The singleton recommendation generation row is missing.");
            }

            int deleted;
            do
            {
                deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE TOP (50000) FROM [dbo].[BookNeighbors] WHERE [GenerationId] <> {activeGenerationId}",
                    cancellationToken);
            }
            while (deleted > 0);

            await transaction.CommitAsync(cancellationToken);
        }

        return activeGenerationId;
    }

    public async Task WriteGenerationAsync(
        Guid generationId,
        IReadOnlyCollection<BookNeighborRow> rows,
        int bookCount,
        DateTime computedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (!dbContext.Database.IsSqlServer())
        {
            throw new InvalidOperationException("Book neighbor generations can only be written to SQL Server.");
        }

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
            await LockCurrentGenerationAsync(connection, sqlTransaction, cancellationToken);

            var rowArray = rows as BookNeighborRow[] ?? rows.ToArray();
            for (var offset = 0; offset < rowArray.Length; offset += BulkBatchSize)
            {
                var count = Math.Min(BulkBatchSize, rowArray.Length - offset);
                var table = CreateTable(generationId, rowArray.AsSpan(offset, count));
                using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, sqlTransaction)
                {
                    DestinationTableName = "[dbo].[BookNeighbors]",
                    BatchSize = BulkBatchSize
                };
                MapNeighborColumns(bulkCopy);
                await bulkCopy.WriteToServerAsync(table, cancellationToken);
            }

            var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [dbo].[RecommendationGenerations]
                   SET [CurrentGenerationId] = {generationId},
                       [BookCount] = {bookCount},
                       [ComputedAt] = {computedAt}
                 WHERE [Id] = 1
                """, cancellationToken);
            if (updated != 1)
            {
                throw new InvalidOperationException("The singleton recommendation generation row is missing.");
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await PruneObsoleteGenerationsAsync(cancellationToken);
    }

    private async Task PruneObsoleteGenerationsAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = (SqlConnection)dbContext.Database.GetDbConnection();
        var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
        var activeGenerationId = await LockCurrentGenerationAsync(connection, sqlTransaction, cancellationToken)
                                ?? throw new InvalidOperationException(
                                    "The singleton recommendation generation has no active generation.");
        int deleted;
        do
        {
            deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE TOP (50000) FROM [dbo].[BookNeighbors] WHERE [GenerationId] <> {activeGenerationId}",
                cancellationToken);
        }
        while (deleted > 0);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<Guid?> LockCurrentGenerationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT [CurrentGenerationId]
              FROM [dbo].[RecommendationGenerations] WITH (UPDLOCK, HOLDLOCK)
             WHERE [Id] = 1
            """, connection, transaction);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null)
        {
            throw new InvalidOperationException("The singleton recommendation generation row is missing.");
        }

        return value is DBNull ? null : (Guid)value;
    }

    private static DataTable CreateTable(Guid generationId, ReadOnlySpan<BookNeighborRow> rows)
    {
        var table = new DataTable();
        table.Columns.Add("GenerationId", typeof(Guid));
        table.Columns.Add("Isbn13", typeof(string));
        table.Columns.Add("Rank", typeof(byte));
        table.Columns.Add("NeighborIsbn13", typeof(string));
        table.Columns.Add("Score", typeof(float));
        table.Columns.Add("Reason", typeof(byte));

        foreach (var row in rows)
        {
            table.Rows.Add(
                generationId,
                row.Isbn13,
                row.Rank,
                row.NeighborIsbn13,
                row.Score,
                (byte)row.Reason);
        }

        return table;
    }

    private static void MapNeighborColumns(SqlBulkCopy bulkCopy)
    {
        bulkCopy.ColumnMappings.Add("GenerationId", "GenerationId");
        bulkCopy.ColumnMappings.Add("Isbn13", "Isbn13");
        bulkCopy.ColumnMappings.Add("Rank", "Rank");
        bulkCopy.ColumnMappings.Add("NeighborIsbn13", "NeighborIsbn13");
        bulkCopy.ColumnMappings.Add("Score", "Score");
        bulkCopy.ColumnMappings.Add("Reason", "Reason");
    }
}
