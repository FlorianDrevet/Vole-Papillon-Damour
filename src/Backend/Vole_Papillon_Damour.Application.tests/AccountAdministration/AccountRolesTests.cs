using FluentAssertions;
using Vole_Papillon_Damour.Application.AccountAdministration;

namespace Vole_Papillon_Damour.Application.tests.AccountAdministration;

public sealed class AccountRolesTests
{
    [Fact]
    public void Normalize_accepts_the_rare_books_role_and_returns_its_canonical_value()
    {
        AccountRoles.Normalize([" livresrares "])
            .Should().Equal("LivresRares");
    }

    [Fact]
    public void IsValid_accepts_the_rare_books_role()
    {
        AccountRoles.IsValid(["LivresRares"])
            .Should().BeTrue();
    }

    [Fact]
    public void Normalize_returns_no_roles_when_any_role_is_unknown()
    {
        AccountRoles.Normalize(["Tri", "RoleInconnu"])
            .Should().BeEmpty();
    }
}
