using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record ExportIsbnNotFoundQuery : IRequest<ErrorOr<IsbnNotFoundCsvExportResult>>;

public sealed record IsbnNotFoundCsvExportResult(byte[] Content, string FileName);
