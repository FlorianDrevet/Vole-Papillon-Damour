using FluentAssertions;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.UserAggregateTests;

public class UserAnonymizationTests
{
    [Fact]
    public void Anonymize_RemovesPersonalIdentityAndRecordsTheTimestamp()
    {
        var user = User.Create(
            "person@example.com",
            "legacy-password",
            new Name("Prénom", "Nom"),
            "legacy-salt");
        var anonymizedAt = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

        user.Anonymize(anonymizedAt);

        user.Email.Should().BeNull();
        user.ExternalId.Should().BeNull();
        user.Name.Should().BeNull();
        user.AnonymizedAt.Should().Be(anonymizedAt);
    }
}
