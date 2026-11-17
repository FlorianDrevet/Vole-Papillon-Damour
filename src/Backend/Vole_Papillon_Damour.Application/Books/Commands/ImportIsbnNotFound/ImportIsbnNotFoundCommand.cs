using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ImportIsbnNotFound;

public sealed record ImportIsbnNotFoundCommand(
    byte[] FileContent,
    IReadOnlyCollection<string> ReadyIsbns,
    UserId ImportedBy) : IRequest<ErrorOr<ImportIsbnNotFoundResult>>;

public sealed record ImportIsbnNotFoundResult(
    int DataRowCount,
    int ImportedCount,
    int WithoutAuthorCount,
    int MarkedAbsentCount,
    int RejectedCount,
    IReadOnlyList<ImportIsbnNotFoundRejectionResult> Rejections);

public sealed record ImportIsbnNotFoundRejectionResult(int RowNumber, string Isbn, string Reason);
