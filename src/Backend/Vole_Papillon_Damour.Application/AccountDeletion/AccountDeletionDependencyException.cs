namespace Vole_Papillon_Damour.Application.AccountDeletion;

public sealed class AccountDeletionDependencyException(string failureCode) : Exception
{
    public string FailureCode { get; } = failureCode;
}
