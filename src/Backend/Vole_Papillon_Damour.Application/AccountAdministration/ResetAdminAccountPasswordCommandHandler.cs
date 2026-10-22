using System.Security.Cryptography;
using System.Text.Json;
using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed class ResetAdminAccountPasswordCommandHandler(
    IEntraAccountPasswordResetDirectory directory)
    : IRequestHandler<ResetAdminAccountPasswordCommand, ErrorOr<AdminAccountPasswordResetResult>>
{
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}:,.?";
    private const string AllCharacters = Lowercase + Uppercase + Digits + Symbols;
    private const int PasswordLength = 24;

    public async Task<ErrorOr<AdminAccountPasswordResetResult>> Handle(
        ResetAdminAccountPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var temporaryPassword = GenerateTemporaryPassword();
        try
        {
            await directory.ResetPasswordAsync(
                command.TargetExternalId.Trim(),
                temporaryPassword,
                cancellationToken);
            return new AdminAccountPasswordResetResult(temporaryPassword);
        }
        catch (EntraAccountDirectoryException exception)
            when (exception.FailureCode == EntraAccountDirectoryException.PasswordResetNotAllowed)
        {
            return Error.Conflict(
                "Account.PasswordResetNotAllowed",
                "A password can only be reset for a local email account with a volunteer role.");
        }
        catch (EntraAccountDirectoryException exception)
        {
            return GetAdminAccountsQueryHandler.ToDirectoryError(exception);
        }
        catch (HttpRequestException)
        {
            return Error.Failure(
                "Account.DirectoryUnavailable",
                "The identity directory is temporarily unavailable.");
        }
        catch (JsonException)
        {
            return Error.Failure(
                "Account.DirectoryUnavailable",
                "The identity directory is temporarily unavailable.");
        }
    }

    private static string GenerateTemporaryPassword()
    {
        var characters = new char[PasswordLength];
        characters[0] = Pick(Lowercase);
        characters[1] = Pick(Uppercase);
        characters[2] = Pick(Digits);
        characters[3] = Pick(Symbols);
        for (var index = 4; index < characters.Length; index++)
        {
            characters[index] = Pick(AllCharacters);
        }

        for (var index = characters.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }

        return new string(characters);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
