using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record PreviewIsbnNotFoundImportQuery(byte[] FileContent)
    : IRequest<ErrorOr<PreviewIsbnNotFoundImportResult>>
{
    public const int MaximumFileSizeBytes = IsbnNotFoundCsvLimits.MaximumFileSizeBytes;
    public const int MaximumDataRows = IsbnNotFoundCsvLimits.MaximumDataRows;
}

public sealed record PreviewIsbnNotFoundImportResult(
    int DataRowCount,
    int ReadyCount,
    int WithoutAuthorCount,
    int IgnoredCount,
    int RejectedCount,
    IReadOnlyList<IsbnNotFoundImportRejectionResult> Rejections,
    IReadOnlyList<string> ReadyIsbns);

public sealed record IsbnNotFoundImportRejectionResult(int RowNumber, string Isbn, string Reason);
