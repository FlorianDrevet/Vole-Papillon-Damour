using System.Globalization;
using System.Text;

namespace Vole_Papillon_Damour.Application.Books.Common;

internal static class IsbnNotFoundCsv
{
    public static readonly string[] Headers =
    [
        "ISBN",
        "Titre",
        "Auteurs",
        "Editeur",
        "Annee_publication",
        "Format",
        "Langue",
        "Genre_principal",
        "Genres",
        "Resume",
        "Pages",
        "Edition",
        "Couverture_URL",
    ];

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static IsbnNotFoundCsvFile Parse(byte[]? content)
    {
        if (content is null || content.Length == 0)
        {
            throw new IsbnNotFoundCsvFormatException("Le fichier CSV est vide.");
        }

        if (content.Length > IsbnNotFoundCsvLimits.MaximumFileSizeBytes)
        {
            throw new IsbnNotFoundCsvFormatException("Le fichier dépasse la limite de 2 Mo.");
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            throw new IsbnNotFoundCsvFormatException("Le fichier doit être encodé en UTF-8.");
        }

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var records = ParseRecords(text);
        if (records.Count == 0)
        {
            throw new IsbnNotFoundCsvFormatException("Le fichier CSV est vide.");
        }

        var columnIndexes = ParseHeader(records[0]);
        var dataRecords = records.Skip(1).ToArray();
        if (dataRecords.Length > IsbnNotFoundCsvLimits.MaximumDataRows)
        {
            throw new IsbnNotFoundCsvFormatException("Le fichier dépasse la limite de 5 000 lignes.");
        }

        var rows = dataRecords.Select(record => ToRow(record, columnIndexes)).ToArray();
        return new IsbnNotFoundCsvFile(rows);
    }

    private static Dictionary<string, int> ParseHeader(CsvRecord header)
    {
        var normalizedHeaders = Headers.ToDictionary(NormalizeHeader, StringComparer.Ordinal);
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var unknown = new List<string>();
        var duplicates = new List<string>();
        for (var index = 0; index < header.Fields.Count; index++)
        {
            var rawHeader = header.Fields[index].Trim();
            var normalized = NormalizeHeader(rawHeader);
            if (!normalizedHeaders.ContainsKey(normalized))
            {
                unknown.Add(string.IsNullOrWhiteSpace(rawHeader) ? "(vide)" : rawHeader);
            }
            else if (!positions.TryAdd(normalized, index))
            {
                duplicates.Add(rawHeader);
            }
        }

        var missing = normalizedHeaders.Keys.Where(name => !positions.ContainsKey(name)).ToArray();
        if (unknown.Count > 0 || duplicates.Count > 0 || missing.Length > 0)
        {
            var details = new List<string>();
            if (missing.Length > 0)
            {
                details.Add($"colonnes manquantes : {string.Join(", ", missing.Select(name => normalizedHeaders[name]))}");
            }

            if (unknown.Count > 0)
            {
                details.Add($"colonnes inconnues : {string.Join(", ", unknown)}");
            }

            if (duplicates.Count > 0)
            {
                details.Add($"colonnes dupliquées : {string.Join(", ", duplicates)}");
            }

            throw new IsbnNotFoundCsvFormatException($"En-tête CSV invalide ({string.Join(" ; ", details)}).");
        }

        return positions;
    }

    private static IsbnNotFoundCsvRow ToRow(CsvRecord record, IReadOnlyDictionary<string, int> columnIndexes)
    {
        string Value(string header)
        {
            var index = columnIndexes[NormalizeHeader(header)];
            return index < record.Fields.Count ? record.Fields[index].Trim() : string.Empty;
        }

        return new IsbnNotFoundCsvRow(
            record.StartLine,
            record.Fields.Count == Headers.Length,
            Value("ISBN"),
            Value("Titre"),
            Value("Auteurs"),
            Value("Editeur"),
            Value("Annee_publication"),
            Value("Format"),
            Value("Langue"),
            Value("Genre_principal"),
            Value("Genres"),
            Value("Resume"),
            Value("Pages"),
            Value("Edition"),
            Value("Couverture_URL"));
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString();
    }

    private static List<CsvRecord> ParseRecords(string text)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var afterQuote = false;
        var recordHasContent = false;
        var lineNumber = 1;
        var recordStartLine = 1;

        void FinishRecord()
        {
            fields.Add(field.ToString());
            records.Add(new CsvRecord(recordStartLine, fields.ToArray()));
            fields = [];
            field.Clear();
            afterQuote = false;
            recordHasContent = false;
            recordStartLine = lineNumber + 1;
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                if (inQuotes)
                {
                    field.Append('\n');
                }
                else
                {
                    FinishRecord();
                }

                lineNumber++;
                continue;
            }

            recordHasContent = true;
            if (character == ';' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
                afterQuote = false;
                continue;
            }

            if (character == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }

                if (inQuotes)
                {
                    inQuotes = false;
                    afterQuote = true;
                    continue;
                }

                if (field.Length == 0 || string.IsNullOrWhiteSpace(field.ToString()))
                {
                    field.Clear();
                    inQuotes = true;
                    continue;
                }

                throw new IsbnNotFoundCsvFormatException($"Guillemets invalides à la ligne {lineNumber}.");
            }

            if (afterQuote)
            {
                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                throw new IsbnNotFoundCsvFormatException($"Guillemets invalides à la ligne {lineNumber}.");
            }

            field.Append(character);
        }

        if (inQuotes)
        {
            throw new IsbnNotFoundCsvFormatException($"Guillemets non fermés à la ligne {recordStartLine}.");
        }

        if (recordHasContent || fields.Count > 0 || field.Length > 0)
        {
            FinishRecord();
        }

        return records;
    }

    private sealed record CsvRecord(int StartLine, IReadOnlyList<string> Fields);
}

internal sealed record IsbnNotFoundCsvFile(IReadOnlyList<IsbnNotFoundCsvRow> Rows);

internal sealed record IsbnNotFoundCsvRow(
    int LineNumber,
    bool HasExpectedColumnCount,
    string Isbn,
    string Title,
    string Authors,
    string Publisher,
    string PublicationYear,
    string PhysicalFormat,
    string Language,
    string Genre,
    string Genres,
    string Summary,
    string Pages,
    string Edition,
    string CoverUrl)
{
    public bool IsCompletelyEmpty => new[]
    {
        Isbn, Title, Authors, Publisher, PublicationYear, PhysicalFormat, Language, Genre,
        Genres, Summary, Pages, Edition, CoverUrl
    }.All(string.IsNullOrWhiteSpace);

    public bool HasNoBookData => new[]
    {
        Title, Authors, Publisher, PublicationYear, PhysicalFormat, Language, Genre,
        Genres, Summary, Pages, Edition, CoverUrl
    }.All(string.IsNullOrWhiteSpace);
}

internal sealed class IsbnNotFoundCsvFormatException(string message) : Exception(message);
