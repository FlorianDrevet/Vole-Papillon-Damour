using System.Security.Claims;
using FluentAssertions;
using Vole_Papillon_Damour.Api.Authentication;

namespace Vole_Papillon_Damour.Api.tests.Authentication;

public sealed class EntraMemberIdentityClaimsReaderTests
{
    [Fact]
    public void TryRead_UsesSeparateNameClaimsWhenNameIsUnknown()
    {
        var principal = CreatePrincipal(
            new Claim("name", "unknown"),
            new Claim("given_name", "Camille"),
            new Claim("family_name", "Dupont"));

        var result = EntraMemberIdentityClaimsReader.TryRead(principal, out var identity);

        result.Should().BeTrue();
        identity.Should().NotBeNull();
        identity!.FirstName.Should().Be("Camille");
        identity.LastName.Should().Be("Dupont");
    }

    [Fact]
    public void TryRead_AcceptsExternalIdAttributeClaimNames()
    {
        var principal = CreatePrincipal(
            new Claim("givenName", "Élodie"),
            new Claim("surname", "Martin"));

        var result = EntraMemberIdentityClaimsReader.TryRead(principal, out var identity);

        result.Should().BeTrue();
        identity.Should().NotBeNull();
        identity!.FirstName.Should().Be("Élodie");
        identity.LastName.Should().Be("Martin");
    }

    [Fact]
    public void TryRead_FallsBackToTheLegacyDisplayNameClaim()
    {
        var principal = CreatePrincipal(new Claim("name", "Camille Dupont"));

        var result = EntraMemberIdentityClaimsReader.TryRead(principal, out var identity);

        result.Should().BeTrue();
        identity.Should().NotBeNull();
        identity!.FirstName.Should().Be("Camille");
        identity.LastName.Should().Be("Dupont");
    }

    [Fact]
    public void TryRead_DoesNotStoreAnUnknownDisplayNameAsARealName()
    {
        var principal = CreatePrincipal(new Claim("name", "unknown"));

        var result = EntraMemberIdentityClaimsReader.TryRead(principal, out var identity);

        result.Should().BeTrue();
        identity.Should().NotBeNull();
        identity!.FirstName.Should().BeNull();
        identity.LastName.Should().BeNull();
    }

    [Fact]
    public void TryRead_RejectsAnIdentityWithoutAnObjectIdOrEmail()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("name", "Camille Dupont"),
        ]));

        var result = EntraMemberIdentityClaimsReader.TryRead(principal, out var identity);

        result.Should().BeFalse();
        identity.Should().BeNull();
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] nameClaims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", "8d12cf6e-958f-4f68-a6f3-914d7bba8cf4"),
            new Claim("email", "camille@example.test"),
            ..nameClaims,
        ]));
    }
}
