using FluentAssertions;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.UserAggregateTests;

public sealed class UserExternalIdentityTests
{
    private static readonly DateTime FirstSeenAt =
        new(2026, 9, 4, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateFromExternalIdentity_StoresOnlyTheIdentityProjection()
    {
        var userId = UserId.Create(Guid.Parse("b8f31286-69f1-4b91-bc85-61c9f2e13e4b"));

        var user = User.CreateFromExternalIdentity(
            userId,
            "oid-value",
            "member@example.test",
            FirstSeenAt);

        user.Id.Should().Be(userId);
        user.ExternalId.Should().Be("oid-value");
        user.Email.Should().Be("member@example.test");
        user.Name.Should().BeNull();
        user.CreatedAt.Should().Be(FirstSeenAt);
        user.LastSeenAt.Should().Be(FirstSeenAt);
        user.AnonymizedAt.Should().BeNull();
    }

    [Fact]
    public void SynchronizeExternalIdentity_RefreshesEmailAndLastSeen()
    {
        var user = User.CreateFromExternalIdentity(
            UserId.CreateUnique(),
            "old-oid",
            "old@example.test",
            FirstSeenAt);
        var lastSeenAt = FirstSeenAt.AddHours(2);

        user.SynchronizeExternalIdentity("new-oid", "new@example.test", lastSeenAt);

        user.ExternalId.Should().Be("new-oid");
        user.Email.Should().Be("new@example.test");
        user.CreatedAt.Should().Be(FirstSeenAt);
        user.LastSeenAt.Should().Be(lastSeenAt);
    }
}
