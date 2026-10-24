using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Isbndb.Commands.CatchUpIsbndbBooks;

public sealed record CatchUpIsbndbBooksResult(
    long RunId,
    int Filled,
    int Absent,
    int Rechecked,
    int Remaining,
    IsbndbCatchUpStopReason StopReason);
