using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.AccountAdministration;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.tests.AccountAdministration;

public sealed class AccountAdministrationHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAdminAccountsQuery_SearchesAndPaginatesDirectoryAccounts()
    {
        var directory = Substitute.For<IEntraAccountDirectory>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        directory.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<EntraAccount>>([
            new("account-1", "zoe@example.test", "Zoé Trieuse", true, Now, [AccountRoles.Tri]),
            new("account-2", "admin@example.test", "Administrateur", true, Now, [AccountRoles.Administration]),
            new("account-3", "caisse@example.test", "Caisse", true, Now, [AccountRoles.Caisse]),
        ]));

        var handler = new GetAdminAccountsQueryHandler(directory, clock);

        var result = await handler.Handle(
            new GetAdminAccountsQuery("admin", 1, 10),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Accounts.Should().ContainSingle(account => account.Email == "admin@example.test");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAdminAccountCommand_NormalizesRolesBeforeCreatingDirectoryAccount()
    {
        var directory = Substitute.For<IEntraAccountDirectory>();
        var expected = new EntraAccount(
            "account-1",
            "marie@example.test",
            "Marie Tri",
            true,
            Now,
            [AccountRoles.Administration, AccountRoles.Tri]);
        directory.CreateAsync(
                "marie@example.test",
                "Marie Tri",
                "Temporaire1!",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        var handler = new CreateAdminAccountCommandHandler(directory);

        var result = await handler.Handle(
            new CreateAdminAccountCommand(
                " Marie@example.test ",
                " Marie Tri ",
                "Temporaire1!",
                [AccountRoles.Tri, AccountRoles.Administration, AccountRoles.Tri]),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("marie@example.test");
        await directory.Received(1).CreateAsync(
            "marie@example.test",
            "Marie Tri",
            "Temporaire1!",
            Arg.Is<IReadOnlyCollection<string>>(roles =>
                roles.SequenceEqual(new[] { AccountRoles.Tri, AccountRoles.Administration })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAdminAccountCommand_RequiresAtLeastOneRole()
    {
        var directory = Substitute.For<IEntraAccountDirectory>();
        var handler = new CreateAdminAccountCommandHandler(directory);

        var result = await handler.Handle(
            new CreateAdminAccountCommand(
                "marie@example.test",
                "Marie Tri",
                "Temporaire1!",
                []),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account.RoleRequired");
        await directory.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default!, default!, default!);
    }

    [Fact]
    public async Task UpdateAdminAccountRolesCommand_CannotRemoveOwnAdministrationRole()
    {
        var directory = Substitute.For<IEntraAccountDirectory>();
        var handler = new UpdateAdminAccountRolesCommandHandler(directory);

        var result = await handler.Handle(
            new UpdateAdminAccountRolesCommand(
                "same-account",
                "same-account",
                [AccountRoles.Tri]),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account.CannotRemoveOwnAdministration");
        await directory.DidNotReceiveWithAnyArgs().SetRolesAsync(default!, default!, default!);
    }

    [Fact]
    public async Task UpdateAdminAccountRolesCommand_SendsSelectedRolesToDirectory()
    {
        var directory = Substitute.For<IEntraAccountDirectory>();
        var expected = new EntraAccount(
            "target-account",
            "tri@example.test",
            "Tri",
            true,
            Now,
            [AccountRoles.Tri]);
        directory.SetRolesAsync(
                "target-account",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        var handler = new UpdateAdminAccountRolesCommandHandler(directory);

        var result = await handler.Handle(
            new UpdateAdminAccountRolesCommand(
                "target-account",
                "admin-account",
                [AccountRoles.Tri, AccountRoles.Tri]),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Roles.Should().ContainSingle().Which.Should().Be(AccountRoles.Tri);
        await directory.Received(1).SetRolesAsync(
            "target-account",
            Arg.Is<IReadOnlyCollection<string>>(roles => roles.SequenceEqual(new[] { AccountRoles.Tri })),
            Arg.Any<CancellationToken>());
    }
}
