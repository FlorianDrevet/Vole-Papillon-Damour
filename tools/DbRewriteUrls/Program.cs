// =======================================================================
// DbRewriteUrls
// -----------------------------------------------------------------------
// Replaces a substring across every textual column of a database. Used to
// repoint the image URLs stored in the rows — they were written by the old
// environment and still name its storage account.
//
// The columns holding those URLs are not declared anywhere, so rather than
// hardcoding a list that would silently miss one, every character column is
// searched. Nothing is written unless --apply is passed, and the counts are
// always reported first.
// =======================================================================

using Microsoft.Data.SqlClient;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: DbRewriteUrls <connection-string> <search> <replacement> [--apply]");
    return 2;
}

var connectionString = args[0];
var search = args[1];
var replacement = args[2];
var apply = args.Contains("--apply");

if (string.IsNullOrEmpty(search))
{
    Console.Error.WriteLine("the search value cannot be empty");
    return 2;
}

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine($"database    : {connection.Database} on {connection.DataSource}");
Console.WriteLine($"replacing   : {search}");
Console.WriteLine($"with        : {replacement}");
Console.WriteLine($"mode        : {(apply ? "APPLY" : "dry run — nothing will be written")}");
Console.WriteLine();

var columns = await ReadTextColumnsAsync(connection);
var total = 0;

foreach (var column in columns)
{
    var matches = await CountMatchesAsync(connection, column, search);

    if (matches == 0)
    {
        continue;
    }

    if (!apply)
    {
        Console.WriteLine($"  {column}: {matches} rows would be rewritten");
        total += matches;
        continue;
    }

    var updated = await RewriteAsync(connection, column, search, replacement);
    var left = await CountMatchesAsync(connection, column, search);

    Console.WriteLine($"  {column}: {updated} rows rewritten, {left} still matching");
    total += updated;
}

Console.WriteLine();
Console.WriteLine(total == 0
    ? "nothing matched — no row references that value"
    : $"{total} rows {(apply ? "rewritten" : "would be rewritten")}");

return 0;

// text and ntext are excluded on purpose: REPLACE cannot be applied to them
// without a cast, and EF Core never maps a property to those legacy types.
static async Task<List<ColumnRef>> ReadTextColumnsAsync(SqlConnection connection)
{
    const string sql = """
        SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE t.is_ms_shipped = 0
          AND c.is_computed = 0
          AND ty.name IN ('char', 'varchar', 'nchar', 'nvarchar')
        ORDER BY s.name, t.name, c.name
        """;

    var columns = new List<ColumnRef>();

    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        columns.Add(new ColumnRef(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
    }

    return columns;
}

static async Task<int> CountMatchesAsync(SqlConnection connection, ColumnRef column, string search)
{
    var sql = $"SELECT COUNT(*) FROM {column.QuotedTable} WHERE {column.QuotedColumn} LIKE @pattern";

    await using var command = new SqlCommand(sql, connection);
    command.Parameters.AddWithValue("@pattern", $"%{Escape(search)}%");

    return (int)(await command.ExecuteScalarAsync())!;
}

static async Task<int> RewriteAsync(SqlConnection connection, ColumnRef column, string search, string replacement)
{
    var sql = $"""
        UPDATE {column.QuotedTable}
        SET {column.QuotedColumn} = REPLACE({column.QuotedColumn}, @search, @replacement)
        WHERE {column.QuotedColumn} LIKE @pattern
        """;

    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
    command.Parameters.AddWithValue("@search", search);
    command.Parameters.AddWithValue("@replacement", replacement);
    command.Parameters.AddWithValue("@pattern", $"%{Escape(search)}%");

    return await command.ExecuteNonQueryAsync();
}

// A host name carries none of these, but the search value is an argument and a
// stray wildcard would silently widen the match.
static string Escape(string value) => value
    .Replace("[", "[[]")
    .Replace("%", "[%]")
    .Replace("_", "[_]");

internal readonly record struct ColumnRef(string Schema, string Table, string Column)
{
    public string QuotedTable => $"[{Schema}].[{Table}]";

    public string QuotedColumn => $"[{Column}]";

    public override string ToString() => $"{Schema}.{Table}.{Column}";
}
