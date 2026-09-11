using FluentAssertions;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.ActualityAggregateTests;

public sealed class SocialPostImportTests
{
    private static readonly DateTimeOffset PublishedAt =
        new(2026, 9, 10, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_StoresTheExternalIdentityAndActualityLink()
    {
        var actualityId = ActualityId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var imported = SocialPostImport.Create(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            SocialPostSource.Instagram,
            " media-42 ",
            new Uri("https://www.instagram.com/p/example/"),
            PublishedAt,
            PublishedAt.AddMinutes(5),
            actualityId);

        imported.Source.Should().Be(SocialPostSource.Instagram);
        imported.ExternalId.Should().Be("media-42");
        imported.ActualityId.Should().Be(actualityId);
    }

    [Fact]
    public void DetachActuality_KeepsTheImportTraceAfterDraftDeletion()
    {
        var imported = SocialPostImport.Create(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            SocialPostSource.Instagram,
            "media-42",
            new Uri("https://www.instagram.com/p/example/"),
            PublishedAt,
            PublishedAt.AddMinutes(5),
            ActualityId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")));

        imported.DetachActuality();

        imported.ActualityId.Should().BeNull();
    }
}
