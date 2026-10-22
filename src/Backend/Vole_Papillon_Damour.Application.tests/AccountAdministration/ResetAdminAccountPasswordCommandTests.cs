using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.AccountAdministration;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.tests.AccountAdministration;

public sealed class ResetAdminAccountPasswordCommandTests
{
    [Fact]
    public async Task Handle_ResetsPasswordAndReturnsTheGeneratedTemporaryPassword()
    {
        var directory = Substitute.For<IEntraAccountPasswordResetDirectory>();
        var handler = new ResetAdminAccountPasswordCommandHandler(directory);

        var result = await handler.Handle(
            new ResetAdminAccountPasswordCommand("volunteer-account"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TemporaryPassword.Should().NotBeNullOrWhiteSpace();
        result.Value.TemporaryPassword.Should().HaveLength(24);
        result.Value.TemporaryPassword.Should().MatchRegex("(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^a-zA-Z0-9])");
        await directory.Received(1).ResetPasswordAsync(
            "volunteer-account",
            result.Value.TemporaryPassword,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTargetIsNotEligible_ReturnsTypedConflict()
    {
        var directory = Substitute.For<IEntraAccountPasswordResetDirectory>();
        directory.ResetPasswordAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new EntraAccountDirectoryException(
                EntraAccountDirectoryException.PasswordResetNotAllowed,
                409)));
        var handler = new ResetAdminAccountPasswordCommandHandler(directory);

        var result = await handler.Handle(
            new ResetAdminAccountPasswordCommand("external-or-non-volunteer"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Account.PasswordResetNotAllowed");
    }
}
