using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.FairDigest.Commands.PlanFairDigest;
using Vole_Papillon_Damour.Application.FairDigest.Commands.SendDueFairDigests;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Application.tests.Books.Queries;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.FairDigest;

public sealed class FairDigestCommandHandlerTests
{
    private static readonly DateTime Now = new(2027, 3, 8, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Plan_CreatesOnlyOneCampaignForTheNextFair()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        await fixture.SaveAsync();
        var handler = new PlanFairDigestCommandHandler(
            fixture.Context,
            Clock(),
            Options.Create(EnabledOptions()),
            RecommendationSettings());

        var first = await handler.Handle(new PlanFairDigestCommand(), CancellationToken.None);
        var second = await handler.Handle(new PlanFairDigestCommand(), CancellationToken.None);

        first.IsError.Should().BeFalse();
        first.Value.Status.Should().Be(FairDigestPlanStatus.Planned);
        first.Value.PlannedAtUtc.Should().Be(DateTimeOffset.Parse("2027-03-10T09:00:00Z"));
        second.Value.Status.Should().Be(FairDigestPlanStatus.AlreadyPlanned);
        (await fixture.Context.FairDigestCampaigns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SendDue_SendsOnceAndIgnoresTheSecondTimerPass()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var fair = fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        var userId = UserId.Create(Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de"));
        var member = User.CreateFromExternalIdentity(
            userId,
            userId.Value.ToString("D"),
            "camille@example.test",
            Now);
        fixture.Context.Users.Add(member);
        var preference = MemberRecommendationPreference.Create(userId, enabled: true, Now);
        preference.GrantFairDigestConsent(Now);
        fixture.Context.MemberRecommendationPreferences.Add(preference);
        Isbn13.TryCreate("9782070408504", out var favoriteIsbn).Should().BeTrue();
        fixture.Context.MemberFavoriteBooks.Add(MemberFavoriteBook.Create(userId, favoriteIsbn, Now));
        var campaign = FairDigestCampaign.Create(fair.Id, Now.AddMinutes(-1));
        fixture.Context.FairDigestCampaigns.Add(campaign);
        await fixture.SaveAsync();

        var content = new FairDigestMemberContent(
            [new PersonalRecommendation(
                Book("9783140464079"),
                NeighborReason.Theme,
                "Le premier favori",
                4)],
            [],
            SkippedNoContent: false);
        var contentService = Substitute.For<IFairDigestContentService>();
        contentService.BuildAsync(
                Arg.Any<User>(),
                Arg.Any<FairDigestScheduleResult>(),
                Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(content));
        var sender = Substitute.For<IFairDigestEmailSender>();
        sender.IsEnabled.Returns(true);
        sender.SendAsync(Arg.Any<FairDigestEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new SendDueFairDigestsCommandHandler(
            fixture.Context,
            Clock(),
            Options.Create(EnabledOptions()),
            RecommendationSettings(),
            sender,
            contentService);

        var first = await handler.Handle(new SendDueFairDigestsCommand(), CancellationToken.None);
        var second = await handler.Handle(new SendDueFairDigestsCommand(), CancellationToken.None);

        first.IsError.Should().BeFalse();
        first.Value.SentCount.Should().Be(1);
        second.Value.SentCount.Should().Be(0);
        await sender.Received(1).SendAsync(
            Arg.Is<FairDigestEmailMessage>(message =>
                message.MemberId == userId &&
                message.CampaignId == campaign.CampaignId &&
                message.Email == "camille@example.test"),
            Arg.Any<CancellationToken>());
        (await fixture.Context.FairDigestDeliveries.SingleAsync()).Status.Should().Be(FairDigestDeliveryStatus.Sent);
    }

    [Fact]
    public async Task SendDue_DoesNotStartASuspendedCampaign()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var fair = fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        var adminId = UserId.Create(Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de"));
        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            adminId,
            adminId.Value.ToString("D"),
            "admin@example.test",
            Now));
        var campaign = FairDigestCampaign.Create(fair.Id, Now.AddMinutes(-1));
        campaign.Suspend(adminId, Now.AddMinutes(-1));
        fixture.Context.FairDigestCampaigns.Add(campaign);
        await fixture.SaveAsync();

        var sender = Substitute.For<IFairDigestEmailSender>();
        sender.IsEnabled.Returns(true);
        var contentService = Substitute.For<IFairDigestContentService>();
        var handler = new SendDueFairDigestsCommandHandler(
            fixture.Context,
            Clock(),
            Options.Create(EnabledOptions()),
            RecommendationSettings(),
            sender,
            contentService);

        var result = await handler.Handle(new SendDueFairDigestsCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CampaignCount.Should().Be(0);
        result.Value.SentCount.Should().Be(0);
        campaign.StartedAt.Should().BeNull();
        await sender.DidNotReceive().SendAsync(
            Arg.Any<FairDigestEmailMessage>(),
            Arg.Any<CancellationToken>());
    }

    private static FairDigestOptions EnabledOptions() => new()
    {
        Enabled = true,
        DaysBefore = 3,
        SendHourParis = 10,
        NewsMax = 8,
        BackMax = 6,
        BackMinCount = 3,
        SoldOutMinSales = 3,
        TopSellersCount = 20,
    };

    private static IRecommendationSettings RecommendationSettings()
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        settings.DefaultMinimumHearts.Returns(3);
        settings.SimilarMinScore.Returns(0.6f);
        settings.HeartThresholds.Returns([0.50f, 0.58f, 0.66f, 0.74f, 0.82f]);
        return settings;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    private static PublicCatalogBookResult Book(string isbn) =>
        new(
            isbn,
            "Livre recommandé",
            "Auteur",
            "Éditeur",
            2020,
            "Paperback",
            "fr",
            "Roman",
            "OL123W",
            null,
            1,
            0,
            null,
            null,
            DateTimeOffset.Parse("2027-03-01T00:00:00Z"),
            DateTimeOffset.Parse("2027-03-01T00:00:00Z"),
            false);
}
