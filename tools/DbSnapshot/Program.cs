// =======================================================================
// DbSnapshot
// -----------------------------------------------------------------------
// Takes the deployed data out of Azure and puts it into the emulated
// environment Aspire runs locally.
//
//   export  runs in CI, where the Azure credentials live, and writes the rows
//           to files that can travel as a build artifact
//   import  runs on a developer machine and fills the local SQL Server and the
//           Azurite blob emulator from those files
//
// Values are written as invariant strings rather than native JSON numbers and
// dates: the import converts them using the target column's own type, so a
// decimal never loses digits to a double and a datetime2 never loses its
// sub-second precision on the way through.
// =======================================================================

using System.Data;
using System.Globalization;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.SqlClient;

const string MigrationsHistoryTable = "__EFMigrationsHistory";

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: DbSnapshot export <sql-connection-string> <directory>");
    Console.Error.WriteLine("       DbSnapshot import <sql-connection-string> <directory> [blob-connection-string] [--force]");
    return 2;
}

var jsonOptions = new JsonSerializerOptions { WriteIndented = false };

switch (args[0])
{
    case "export":
        return await ExportAsync(args[1], args[2]);

    case "import":
        var blobConnectionString = args.Length >= 4 && !args[3].StartsWith("--") ? args[3] : null;
        return await ImportAsync(args[1], args[2], blobConnectionString, args.Contains("--force"));

    default:
        Console.Error.WriteLine("unknown verb — expected export or import");
        return 2;
}

async Task<int> ExportAsync(string connectionString, string directory)
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var dataDirectory = Path.Combine(directory, "data");
    Directory.CreateDirectory(dataDirectory);

    Console.WriteLine($"exporting {connection.Database} from {connection.DataSource}");
    Console.WriteLine();

    foreach (var table in (await ReadTablesAsync(connection)).OrderBy(table => table.ToString()))
    {
        if (string.Equals(table.Name, MigrationsHistoryTable, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var columns = await ReadCopyableColumnsAsync(connection, table);

        if (columns.Count == 0)
        {
            continue;
        }

        var columnList = string.Join(", ", columns.Select(column => $"[{column}]"));
        var rows = new List<string?[]>();

        await using (var command = new SqlCommand($"SELECT {columnList} FROM {table.Quoted}", connection) { CommandTimeout = 0 })
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var row = new string?[columns.Count];

                for (var i = 0; i < columns.Count; i++)
                {
                    row[i] = reader.IsDBNull(i) ? null : Stringify(reader.GetValue(i));
                }

                rows.Add(row);
            }
        }

        var path = Path.Combine(dataDirectory, $"{table.Schema}.{table.Name}.json");
        var payload = new TablePayload(table.Schema, table.Name, columns, rows);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, jsonOptions);

        Console.WriteLine($"  {table}: {rows.Count} rows");
    }

    Console.WriteLine();
    Console.WriteLine("export complete");
    return 0;
}

async Task<int> ImportAsync(string connectionString, string directory, string? blobConnectionString, bool force)
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    Console.WriteLine($"importing into {connection.Database} on {connection.DataSource}");
    Console.WriteLine();

    var dataDirectory = Path.Combine(directory, "data");
    var failed = false;

    if (Directory.Exists(dataDirectory))
    {
        var targetTables = await ReadTablesAsync(connection);

        // Foreign keys are switched off for the load and switched back on WITH
        // CHECK at the end, so re-enabling doubles as the integrity check.
        await SetConstraintsAsync(connection, targetTables, enabled: false);

        try
        {
            foreach (var path in Directory.EnumerateFiles(dataDirectory, "*.json").OrderBy(path => path))
            {
                await using var stream = File.OpenRead(path);
                var payload = await JsonSerializer.DeserializeAsync<TablePayload>(stream, jsonOptions);

                if (payload is null)
                {
                    continue;
                }

                var table = new TableName(payload.Schema, payload.Table);

                if (!targetTables.Contains(table))
                {
                    Console.WriteLine($"! {table}: not present locally — skipped");
                    continue;
                }

                if (payload.Rows.Count == 0)
                {
                    Console.WriteLine($"- {table}: nothing to load");
                    continue;
                }

                var existing = await CountRowsAsync(connection, table);

                if (existing > 0 && !force)
                {
                    Console.Error.WriteLine($"x {table}: already holds {existing} rows — pass --force to load anyway");
                    failed = true;
                    continue;
                }

                var loaded = await LoadTableAsync(connection, table, payload);
                Console.WriteLine($"+ {table}: {loaded} rows loaded");
            }
        }
        finally
        {
            await SetConstraintsAsync(connection, targetTables, enabled: true);
        }
    }

    var blobDirectory = Path.Combine(directory, "blobs");

    if (blobConnectionString is not null && Directory.Exists(blobDirectory))
    {
        Console.WriteLine();
        await UploadBlobsAsync(blobConnectionString, blobDirectory);
    }

    Console.WriteLine();
    Console.WriteLine(failed ? "IMPORT FAILED" : "IMPORT OK");
    return failed ? 1 : 0;
}

static async Task UploadBlobsAsync(string blobConnectionString, string blobDirectory)
{
    var service = new BlobServiceClient(blobConnectionString);

    foreach (var containerDirectory in Directory.EnumerateDirectories(blobDirectory))
    {
        var containerName = Path.GetFileName(containerDirectory);
        var container = service.GetBlobContainerClient(containerName);

        // The emulator starts empty, so the containers are created with the
        // same anonymous read access the deployed ones have: the app hands the
        // raw blob URL to the browser.
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var uploaded = 0;

        foreach (var file in Directory.EnumerateFiles(containerDirectory, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(containerDirectory, file).Replace(Path.DirectorySeparatorChar, '/');

            await using var stream = File.OpenRead(file);
            await container.GetBlobClient(name).UploadAsync(stream, overwrite: true);
            uploaded++;
        }

        Console.WriteLine($"+ {containerName}: {uploaded} blobs uploaded");
    }
}

static async Task<int> LoadTableAsync(SqlConnection connection, TableName table, TablePayload payload)
{
    // The column types come from the local table rather than the file, so each
    // string is turned back into exactly what the column expects.
    var types = await ReadColumnTypesAsync(connection, table);
    var usable = payload.Columns.Where(types.ContainsKey).ToList();

    var data = new DataTable();

    foreach (var column in usable)
    {
        data.Columns.Add(column, types[column]);
    }

    foreach (var row in payload.Rows)
    {
        var values = new object[usable.Count];

        for (var i = 0; i < usable.Count; i++)
        {
            var index = payload.Columns.IndexOf(usable[i]);
            values[i] = Parse(row[index], types[usable[i]]);
        }

        data.Rows.Add(values);
    }

    using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, externalTransaction: null)
    {
        DestinationTableName = table.Quoted,
        BulkCopyTimeout = 0,
    };

    foreach (var column in usable)
    {
        bulkCopy.ColumnMappings.Add(column, column);
    }

    await bulkCopy.WriteToServerAsync(data);
    return data.Rows.Count;
}

static string Stringify(object value) => value switch
{
    byte[] bytes => Convert.ToBase64String(bytes),
    DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
    DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
    TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
    Guid guid => guid.ToString("D"),
    bool flag => flag ? "true" : "false",
    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString() ?? string.Empty,
};

static object Parse(string? value, Type type)
{
    if (value is null)
    {
        return DBNull.Value;
    }

    if (type == typeof(byte[]))
    {
        return Convert.FromBase64String(value);
    }

    if (type == typeof(DateTime))
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    if (type == typeof(DateTimeOffset))
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    if (type == typeof(TimeSpan))
    {
        return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    }

    if (type == typeof(Guid))
    {
        return Guid.Parse(value);
    }

    if (type == typeof(bool))
    {
        return bool.Parse(value);
    }

    if (type == typeof(string))
    {
        return value;
    }

    return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
}

static async Task<Dictionary<string, Type>> ReadColumnTypesAsync(SqlConnection connection, TableName table)
{
    await using var command = new SqlCommand($"SELECT TOP 0 * FROM {table.Quoted}", connection);
    await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly);

    var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < reader.FieldCount; i++)
    {
        types[reader.GetName(i)] = reader.GetFieldType(i);
    }

    return types;
}

static async Task<HashSet<TableName>> ReadTablesAsync(SqlConnection connection)
{
    const string sql = """
        SELECT s.name, t.name
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

static async Task<List<string>> ReadCopyableColumnsAsync(SqlConnection connection, TableName table)
{
    const string sql = """
        SELECT c.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = @schema AND t.name = @table
          AND c.is_computed = 0
          AND c.system_type_id <> TYPE_ID('timestamp')
        ORDER BY c.column_id
        """;

    var columns = new List<string>();

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

// Azure SQL Database does not ship sp_MSforeachtable, and neither does a plain
// container, so the statements are generated from the table list.
static async Task SetConstraintsAsync(SqlConnection connection, IEnumerable<TableName> tables, bool enabled)
{
    foreach (var table in tables)
    {
        var sql = enabled
            ? $"ALTER TABLE {table.Quoted} WITH CHECK CHECK CONSTRAINT ALL"
            : $"ALTER TABLE {table.Quoted} NOCHECK CONSTRAINT ALL";

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync();
    }
}

internal readonly record struct TableName(string Schema, string Name)
{
    public string Quoted => $"[{Schema}].[{Name}]";

    public override string ToString() => $"{Schema}.{Name}";
}

internal sealed record TablePayload(string Schema, string Table, List<string> Columns, List<string?[]> Rows);
