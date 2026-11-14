using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MediatR;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Application.tests.Books.Queries;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.tests.FairDigest;

public sealed class FairDigestContentServiceTests
{
    private static readonly DateTime Now = new(2027, 3, 8, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Build_ExcludesBooksOnTheMemberSearchList()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var previousFair = fixture.AddFair(
            "Bourse de février",
            DateTimeOffset.Parse("2027-01-31T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-02-01T18:00:00+01:00"));
        var nextFair = fixture.AddFair(
            "Bourse de mars",
            DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"),
            DateTimeOffset.Parse("2027-03-13T18:00:00+01:00"));
        var watchedBook = await AddAvailableBookAsync(fixture, "9782070363735", "OL-WATCHED", "Livre suivi");
        var otherBook = await AddAvailableBookAsync(fixture, "9783140464079", "OL-OTHER", "Autre livre");
        await fixture.AddBookEntryMovementAsync(
            watchedBook.Id.Value,
            new DateTime(2027, 2, 10, 10, 0, 0, DateTimeKind.Utc));
        await fixture.AddBookEntryMovementAsync(
            otherBook.Id.Value,
            new DateTime(2027, 2, 11, 10, 0, 0, DateTimeKind.Utc));

        var userId = UserId.Create(Guid.Parse("7f0b0f0e-0000-0000-0000-000000000c01"));
        var member = User.CreateFromExternalIdentity(userId, userId.Value.ToString("D"), "member@example.test", Now);
        fixture.Context.Users.Add(member);
        fixture.Context.MemberRecommendationPreferences.Add(MemberRecommendationPreference.Create(userId, true, Now));
        fixture.Context.WatchlistItems.Add(WatchlistItem.CreateWork(
            Guid.NewGuid(), userId, "OL-WATCHED", Now));
        await fixture.SaveAsync();

        var recommendations = new[]
        {
            new PersonalRecommendation(PublicBook(watchedBook.Id.Value, "OL-WATCHED"), NeighborReason.Theme, "Favori", 4),
            new PersonalRecommendation(PublicBook(otherBook.Id.Value, "OL-OTHER"), NeighborReason.SameAuthor, "Favori", 3),
        };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetMyRecommendationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new MyRecommendationsResult(RecommendationStatus.Enabled, recommendations)));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var service = new FairDigestContentService(
            fixture.Context,
            mediator,
            clock,
            Options.Create(new FairDigestOptions()),
            new FairDigestContentBuilder(Options.Create(new FairDigestOptions())));
        var previousEvent = new FairDigestEvent(
            previousFair.Id.Value,
            EventsType.EventsTypeEnum.Books,
            previousFair.DateStart,
            previousFair.DateEnd,
            false,
            previousFair.Name);
        var nextEvent = new FairDigestEvent(
            nextFair.Id.Value,
            EventsType.EventsTypeEnum.Books,
            nextFair.DateStart,
            nextFair.DateEnd,
            false,
            nextFair.Name);
        var schedule = new FairDigestScheduleResult(
            nextEvent,
            previousEvent,
            DateTimeOffset.Parse("2027-03-10T09:00:00Z"),
            FairDigestSchedule.EndOfFairDay(previousFair.DateEnd!.Value));

        var result = await service.BuildAsync(member, schedule, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.News.Should().ContainSingle();
        result.Value.News[0].Book.Isbn13.Should().Be(otherBook.Id.Value);
        result.Value.Back.Should().BeEmpty();
    }

    private static PublicCatalogBookResult PublicBook(string isbn, string workId) => new(
        isbn,
        "Livre recommandé",
        "Auteur",
        "Éditeur",
        2020,
        "Paperback",
        "fr",
        "Roman",
        workId,
        null,
        1,
        0,
        null,
        null,
        DateTimeOffset.Parse("2027-02-10T00:00:00Z"),
        DateTimeOffset.Parse("2027-02-10T00:00:00Z"),
        false);

    private static async Task<Book> AddAvailableBookAsync(
        CatalogDeltaFixture fixture,
        string isbn,
        string workId,
        string title)
    {
        Isbn13.TryCreate(isbn, out var isbn13).Should().BeTrue();
        var updatedAt = GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1);
        var book = Book.Create(isbn13, updatedAt);
        book.RecordAvailableEntry(updatedAt);
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                title,
                "Auteur",
                "Éditeur",
                2020,
                "Poche",
                "fr",
                "Roman",
                null,
                [BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.WorkId],
                workId),
            updatedAt);
        fixture.Context.Books.Add(book);
        await fixture.SaveAsync();
        return book;
    }
}
