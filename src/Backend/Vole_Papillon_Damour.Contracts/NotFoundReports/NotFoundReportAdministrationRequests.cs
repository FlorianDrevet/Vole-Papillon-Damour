namespace Vole_Papillon_Damour.Contracts.NotFoundReports;

public sealed record NotFoundReportFoundRequest(string? Note);

public sealed record NotFoundReportDismissalRequest(string Note);

public sealed record NotFoundReportWithdrawalRequest(
    int QuantityFound,
    string Reason,
    string Note);

public sealed record RareBookNotFoundReportWithdrawalRequest(
    string Reason,
    string Note);
