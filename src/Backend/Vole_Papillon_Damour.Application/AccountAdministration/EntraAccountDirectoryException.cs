namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed class EntraAccountDirectoryException(
    string failureCode,
    int? statusCode = null) : Exception(failureCode)
{
    public const string TemporaryPasswordRejected = "graph-temporary-password-rejected";

    public string FailureCode { get; } = failureCode;

    public int? StatusCode { get; } = statusCode;
}
