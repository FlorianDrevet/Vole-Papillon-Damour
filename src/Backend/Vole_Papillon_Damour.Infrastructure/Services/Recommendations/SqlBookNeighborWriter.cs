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
                bulkCopy.ColumnMappings.Add("GenerationId", "GenerationId");
                bulkCopy.ColumnMappings.Add("Isbn13", "Isbn13");
                bulkCopy.ColumnMappings.Add("Rank", "Rank");
                bulkCopy.ColumnMappings.Add("NeighborIsbn13", "NeighborIsbn13");
                bulkCopy.ColumnMappings.Add("Score", "Score");
                bulkCopy.ColumnMappings.Add("Reason", "Reason");
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

        int deleted;
        do
        {
            deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE TOP (50000) FROM [dbo].[BookNeighbors] WHERE [GenerationId] <> {generationId}",
                cancellationToken);
        }
        while (deleted > 0);
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
}
