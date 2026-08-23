// =======================================================================
// DbImport
// -----------------------------------------------------------------------
// Copies the rows of every user table from one database into another that
// already carries the schema. Used to bring the production data into the
// database the Container App points at, without touching production itself:
// the source is a copy of it, sitting on the same server as the target, so a
// single set of credentials reaches both and the original is never connected
// to.
//
// The EF migrations history is deliberately left alone — the target's own
// history describes the schema that is actually there.
// =======================================================================

using Microsoft.Data.SqlClient;

const string MigrationsHistoryTable = "__EFMigrationsHistory";

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: DbImport <source-connection-string> <target-connection-string> [--force]");
    return 2;
}

var sourceConnectionString = args[0];
var targetConnectionString = args[1];

// Without --force the import refuses to touch a table that already holds rows,
// so a second run cannot silently duplicate everything.
var force = args.Contains("--force");

await using var source = new SqlConnection(sourceConnectionString);
await using var target = new SqlConnection(targetConnectionString);
await source.OpenAsync();
await target.OpenAsync();

Console.WriteLine($"source : {source.Database} on {source.DataSource}");
Console.WriteLine($"target : {target.Database} on {target.DataSource}");
Console.WriteLine();

var sourceTables = await ReadTablesAsync(source);
var targetTables = await ReadTablesAsync(target);

var tables = targetTables
    .Where(table => sourceTables.Contains(table))
    .Where(table => !string.Equals(table.Name, MigrationsHistoryTable, StringComparison.OrdinalIgnoreCase))
    .OrderBy(table => table.Schema, StringComparer.OrdinalIgnoreCase)
    .ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();

var missingInTarget = sourceTables
    .Where(table => !targetTables.Contains(table))
    .Where(table => !string.Equals(table.Name, MigrationsHistoryTable, StringComparison.OrdinalIgnoreCase))
    .ToList();

foreach (var table in missingInTarget)
{
    Console.WriteLine($"! {table} exists in the source but not in the target — skipped");
}

// Rows are inserted in whatever order the tables come in, so foreign keys are
// switched off for the duration and switched back on WITH CHECK at the end:
// re-enabling is itself the integrity verification.
await ExecuteAsync(target, "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

var report = new List<(string Table, long Source, long Target)>();
var failed = false;

try
{
    foreach (var table in tables)
    {
        var sourceRows = await CountRowsAsync(source, table);
        var targetRows = await CountRowsAsync(target, table);

        if (sourceRows == 0)
        {
            Console.WriteLine($"- {table}: source is empty — nothing to copy");
            report.Add((table.ToString(), sourceRows, targetRows));
            continue;
        }

        if (targetRows > 0 && !force)
        {
            Console.Error.WriteLine($"x {table}: target already holds {targetRows} rows — refusing to append (pass --force to override)");
            failed = true;
            continue;
        }

        var columns = await ReadCopyableColumnsAsync(source, table);
        columns.IntersectWith(await ReadCopyableColumnsAsync(target, table));

        if (columns.Count == 0)
        {
            Console.Error.WriteLine($"x {table}: no column in common between source and target");
            failed = true;
            continue;
        }

        var copied = await CopyTableAsync(source, target, table, columns);
        var afterRows = await CountRowsAsync(target, table);

        Console.WriteLine($"+ {table}: {copied} rows copied over {columns.Count} columns — target now holds {afterRows}");
        report.Add((table.ToString(), sourceRows, afterRows));
    }
}
finally
{
    // WITH CHECK makes SQL Server validate every existing row against the
    // foreign keys, so a broken reference surfaces here rather than at runtime.
    await ExecuteAsync(target, "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'");
}

Console.WriteLine();
Console.WriteLine($"{"table",-40} {"source",10} {"target",10}");
Console.WriteLine(new string('-', 62));

foreach (var (table, sourceRows, targetRows) in report)
{
    var flag = sourceRows == targetRows ? " " : "!";
    Console.WriteLine($"{flag}{table,-39} {sourceRows,10} {targetRows,10}");
}

var mismatched = report.Where(row => row.Source != row.Target).ToList();

if (mismatched.Count > 0)
{
    Console.Error.WriteLine();

    foreach (var (table, sourceRows, targetRows) in mismatched)
    {
        Console.Error.WriteLine($"x {table}: {sourceRows} rows in the source but {targetRows} in the target");
    }

    failed = true;
}

Console.WriteLine();
Console.WriteLine(failed ? "IMPORT FAILED" : "IMPORT OK");

return failed ? 1 : 0;

static async Task<HashSet<TableName>> ReadTablesAsync(SqlConnection connection)
{
    const string sql = """
        SELECT s.name AS SchemaName, t.name AS TableName
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
        """;

    var tables = new HashSet<TableName>();

    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        tables.Add(new TableName(reader.GetString(0), reader.GetString(1)));
    }

    return tables;
}

// Computed columns and rowversions are produced by the engine: selecting them
// is fine but writing them is rejected, so they never take part in the copy.
static async Task<HashSet<string>> ReadCopyableColumnsAsync(SqlConnection connection, TableName table)
{
    const string sql = """
        SELECT c.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = @schema AND t.name = @table
          AND c.is_computed = 0
          AND c.system_type_id <> TYPE_ID('timestamp')
        """;

    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    await using var command = new SqlCommand(sql, connection);
    command.Parameters.AddWithValue("@schema", table.Schema);
    command.Parameters.AddWithValue("@table", table.Name);

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        columns.Add(reader.GetString(0));
    }

    return columns;
}

static async Task<long> CountRowsAsync(SqlConnection connection, TableName table)
{
    await using var command = new SqlCommand($"SELECT COUNT_BIG(*) FROM {table.Quoted}", connection);
    return (long)(await command.ExecuteScalarAsync())!;
}

static async Task<long> CopyTableAsync(
    SqlConnection source,
    SqlConnection target,
    TableName table,
    HashSet<string> columns)
{
    var ordered = columns.OrderBy(column => column, StringComparer.OrdinalIgnoreCase).ToList();
    var columnList = string.Join(", ", ordered.Select(column => $"[{column}]"));

    await using var command = new SqlCommand($"SELECT {columnList} FROM {table.Quoted}", source)
    {
        CommandTimeout = 0,
    };

    await using var reader = await command.ExecuteReaderAsync();

    // KeepIdentity preserves the primary keys, which is what makes the foreign
    // keys between the copied tables still line up.
    using var bulkCopy = new SqlBulkCopy(target, SqlBulkCopyOptions.KeepIdentity, externalTransaction: null)
    {
        DestinationTableName = table.Quoted,
        BulkCopyTimeout = 0,
        BatchSize = 1000,
    };

    foreach (var column in ordered)
    {
        bulkCopy.ColumnMappings.Add(column, column);
    }

    await bulkCopy.WriteToServerAsync(reader);

    return bulkCopy.RowsCopied64;
}

static async Task ExecuteAsync(SqlConnection connection, string sql)
{
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
    await command.ExecuteNonQueryAsync();
}

internal readonly record struct TableName(string Schema, string Name)
{
    public string Quoted => $"[{Schema}].[{Name}]";

    public override string ToString() => $"{Schema}.{Name}";
}
