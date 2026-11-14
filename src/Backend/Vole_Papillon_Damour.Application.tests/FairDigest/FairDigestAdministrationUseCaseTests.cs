using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.FairDigest.Commands.ResumeFairDigest;
using Vole_Papillon_Damour.Application.FairDigest.Commands.SendFairDigestTest;
using Vole_Papillon_Damour.Application.FairDigest.Commands.SuspendFairDigest;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.FairDigest.Queries.GetFairDigestStatus;
using Vole_Papillon_Damour.Application.FairDigest.Queries.PreviewFairDigest;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Application.tests.Books.Queries;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.FairDigest;

public sealed class FairDigestAdministrationUseCaseTests
{
    private static readonly DateTime Now = new(2027, 3, 8, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SuspendAndResume_ChangeTheUpcomingCampaignUntilItStarts()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var fair = fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        var adminId = AdministratorId();
        fixture.Context.Users.Add(NewAdmin(adminId));
        var campaign = FairDigestCampaign.Create(fair.Id, Now);
        fixture.Context.FairDigestCampaigns.Add(campaign);
        await fixture.SaveAsync();

        var suspend = await new SuspendFairDigestCommandHandler(fixture.Context, Clock()).Handle(
            new SuspendFairDigestCommand(adminId),
            CancellationToken.None);
        var resume = await new ResumeFairDigestCommandHandler(fixture.Context, Clock()).Handle(
            new ResumeFairDigestCommand(adminId),
            CancellationToken.None);

        suspend.IsError.Should().BeFalse();
        suspend.Value.Changed.Should().BeTrue();
        resume.IsError.Should().BeFalse();
        resume.Value.Changed.Should().BeTrue();
        var stored = await fixture.Context.FairDigestCampaigns.SingleAsync();
        stored.SuspendedAt.Should().BeNull();
        stored.SuspendedBy.Should().BeNull();
    }

    [Fact]
    public async Task Resume_ReturnsConflictWhenTheCampaignHasStarted()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var fair = fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        var adminId = AdministratorId();
        fixture.Context.Users.Add(NewAdmin(adminId));
        var campaign = FairDigestCampaign.Create(fair.Id, Now);
        campaign.Start(Now);
        fixture.Context.FairDigestCampaigns.Add(campaign);
        await fixture.SaveAsync();

        var result = await new ResumeFairDigestCommandHandler(fixture.Context, Clock()).Handle(
            new ResumeFairDigestCommand(adminId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FairDigest.CampaignAlreadyStarted");
    }

    [Fact]
    public async Task Status_ReturnsTheUpcomingFairAndEstimatedRecipientCount()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var fair = fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        await fixture.SaveAsync();
        var sender = Substitute.For<IFairDigestEmailSender>();
        sender.IsEnabled.Returns(true);

        var result = await new GetFairDigestStatusQueryHandler(
                fixture.Context,
                Clock(),
                Options.Create(new FairDigestOptions { Enabled = true }),
                RecommendationSettings(),
                sender)
            .Handle(new GetFairDigestStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Enabled.Should().BeTrue();
        result.Value.NextFairId.Should().Be(fair.Id.Value);
        result.Value.NextFairName.Should().Be("Bourse de mars");
        result.Value.PlannedAtUtc.Should().Be(DateTimeOffset.Parse("2027-03-10T09:00:00Z"));
        result.Value.EstimatedRecipients.Should().Be(0);
        result.Value.LastReport.Should().BeNull();
    }

    [Fact]
    public async Task Preview_UsesTheExampleWhenTheAdministratorHasNoFavorites()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var fair = fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        var adminId = AdministratorId();
        fixture.Context.Users.Add(NewAdmin(adminId));
        await fixture.SaveAsync();
        var contentService = Substitute.For<IFairDigestContentService>();

        var result = await new PreviewFairDigestQueryHandler(
                fixture.Context,
                Clock(),
                Options.Create(new FairDigestOptions()),
                contentService)
            .Handle(new PreviewFairDigestQuery(adminId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Fair.Id.Should().Be(fair.Id.Value);
        result.Value.IsExample.Should().BeTrue();
        result.Value.Content.News.Should().ContainSingle();
        await contentService.DidNotReceive().BuildAsync(
            Arg.Any<User>(),
            Arg.Any<FairDigestScheduleResult>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendTest_SendsOnlyToTheAdministratorAddress()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var adminId = AdministratorId();
        fixture.Context.Users.Add(NewAdmin(adminId));
        await fixture.SaveAsync();

        var fair = new FairDigestEvent(
            Guid.Parse("a83c8586-6c92-40a4-a3ab-0f74515b20d7"),
            EventsType.EventsTypeEnum.Books,
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"),
            false,
            "Bourse de mars");
        var content = new FairDigestMemberContent(
            [new PersonalRecommendation(Book(), NeighborReason.Theme, "Le Petit Prince", 4)],
            [],
            SkippedNoContent: false);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<PreviewFairDigestQuery>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new FairDigestPreviewResult(
                Guid.NewGuid(), fair, content, IsExample: true)));
        var sender = Substitute.For<IFairDigestEmailSender>();
        sender.IsEnabled.Returns(true);
        sender.SendAsync(Arg.Any<FairDigestEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        var handler = new SendFairDigestTestCommandHandler(
            fixture.Context,
            mediator,
            Options.Create(new FairDigestOptions { Enabled = true }),
            settings,
            sender);

        var result = await handler.Handle(new SendFairDigestTestCommand(adminId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("admin@example.test");
        result.Value.IsExample.Should().BeTrue();
        await sender.Received(1).SendAsync(
            Arg.Is<FairDigestEmailMessage>(message =>
                message.MemberId == adminId &&
                message.Email == "admin@example.test" &&
                message.Fair.Id == fair.Id),
            Arg.Any<CancellationToken>());
    }

    private static UserId AdministratorId() =>
        UserId.Create(Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de"));

    private static User NewAdmin(UserId id) => User.CreateFromExternalIdentity(
        id,
        id.Value.ToString("D"),
        "admin@example.test",
        Now);

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    private static IRecommendationSettings RecommendationSettings()
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        settings.DefaultMinimumHearts.Returns(3);
        settings.SimilarMinScore.Returns(0.6f);
        settings.HeartThresholds.Returns([0.50f, 0.58f, 0.66f, 0.74f, 0.82f]);
        return settings;
    }

    private static PublicCatalogBookResult Book() => new(
        "9782070363735",
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
