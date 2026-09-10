using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Actuality.Commands.PublishActuality;
using Vole_Papillon_Damour.Application.Actuality.Queries.GetActualityById;
using Vole_Papillon_Damour.Application.Actuality.Queries.GetAllActuality;
using Vole_Papillon_Damour.Application.Actuality.Queries;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;
using ActualityAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.Actuality;

namespace Vole_Papillon_Damour.Application.tests.Actuality;

public sealed class ActualityQueryHandlerTests
{
    private static readonly DateTimeOffset FirstDate =
        new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly Uri PrincipalImage = new("https://cdn.example.test/principal.jpg");

    [Fact]
    public async Task GetAll_DefaultQuery_ReturnsOnlyPublishedActualities()
    {
        var published = CreateManual("Publiée", FirstDate);
        var draft = CreateImported("Brouillon", FirstDate.AddDays(1));
        var repository = Substitute.For<IActualityRepository>();
        repository.GetAllAsync().Returns([published, draft]);

        var handler = new GetAllActualityQueryHandler(repository, TestMapperFactory.Create());

        var result = await handler.Handle(new GetAllActualityQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle(actuality => actuality.Title == "Publiée");
    }

    [Fact]
    public async Task GetAll_WhenDraftsAreRequested_ReturnsPublishedAndDraftActualities()
    {
        var published = CreateManual("Publiée", FirstDate);
        var draft = CreateImported("Brouillon", FirstDate.AddDays(1));
        var repository = Substitute.For<IActualityRepository>();
        repository.GetAllAsync().Returns([published, draft]);

        var handler = new GetAllActualityQueryHandler(repository, TestMapperFactory.Create());

        var result = await handler.Handle(
            new GetAllActualityQuery(IncludeDrafts: true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Select(actuality => actuality.Title)
            .Should()
            .BeEquivalentTo("Publiée", "Brouillon");
    }

    [Fact]
    public async Task GetById_WhenActualityIsDraft_ReturnsNotFound()
    {
        var draft = CreateImported("Brouillon", FirstDate);
        var repository = Substitute.For<IActualityRepository>();
        repository.GetByIdAsync(draft.Id).Returns(draft);

        var handler = new GetActualityByIdQueryHandler(repository, TestMapperFactory.Create());

        var result = await handler.Handle(
            new GetActualityByIdQuery(draft.Id),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Actuality.NotFound");
    }

    [Fact]
    public async Task GetLatest_WhenRepositoryReturnsDrafts_ReturnsOnlyPublishedActualities()
    {
        var published = CreateManual("Publiée", FirstDate);
        var draft = CreateImported("Brouillon", FirstDate.AddDays(1));
        var repository = Substitute.For<IActualityRepository>();
        repository.GetLatestActualityAsync().Returns([published, draft]);

        var handler = new GetLatestActualityQueryHandler(repository, TestMapperFactory.Create());

        var result = await handler.Handle(new GetLatestActualityQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle(actuality => actuality.Title == "Publiée");
    }

    [Fact]
    public async Task Publish_WhenDraftIsComplete_PublishesItAndClearsTitleReview()
    {
        var draft = CreateImported("Titre proposé", FirstDate);
        var repository = Substitute.For<IActualityRepository>();
        repository.GetByIdAsync(draft.Id).Returns(draft);
        repository.UpdateAsync(draft).Returns(draft);

        var handler = new PublishActualityCommandHandler(repository, TestMapperFactory.Create());

        var result = await handler.Handle(
            new PublishActualityCommand(draft.Id),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        draft.Status.Should().Be(ActualityStatus.Published);
        draft.TitleNeedsReview.Should().BeFalse();
        await repository.Received(1).UpdateAsync(draft);
    }

    private static ActualityAggregate CreateManual(string title, DateTimeOffset date) =>
        ActualityAggregate.Create(title, "Article", PrincipalImage, null, null, [], date);

    private static ActualityAggregate CreateImported(string title, DateTimeOffset date) =>
        ActualityAggregate.CreateImported(title, "Article", PrincipalImage, null, [], date, date, true);
}
