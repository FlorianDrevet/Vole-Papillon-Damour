using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Common;

public interface INotFoundReportLapser
{
    Task LapseIfUnavailableAsync(Isbn13 isbn13, DateTime at, CancellationToken ct);
    Task LapseRareBookAsync(RareBookId rareBookId, DateTime at, CancellationToken ct);
}
