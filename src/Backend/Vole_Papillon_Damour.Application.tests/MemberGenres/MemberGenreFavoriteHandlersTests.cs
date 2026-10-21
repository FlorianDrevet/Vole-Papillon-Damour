using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberGenres.Commands.SetMyGenreFavorite;
using Vole_Papillon_Damour.Application.MemberGenres.Queries.GetMyGenreFavorites;
using Vole_Papillon_Damour.Application.tests.CheckoutPassages;

namespace Vole_Papillon_Damour.Application.tests.MemberGenres;

public sealed class MemberGenreFavoriteHandlersTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SetAndGetFavorites_StoresGenresForTheSignedInMemberAndCanRemoveThem()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var identity = member.Id.Value.ToString("D");
        var identityService = new MemberIdentityService(fixture.Context, new CheckoutPassageTestClock(Now));
        var setHandler = new SetMyGenreFavoriteCommandHandler(fixture.Context, identityService);
        var getHandler = new GetMyGenreFavoritesQueryHandler(fixture.Context, identityService);

        var added = await setHandler.Handle(
            new SetMyGenreFavoriteCommand(identity, member.Email, null, null, "  Manga  ", true),
            CancellationToken.None);
        var duplicate = await setHandler.Handle(
            new SetMyGenreFavoriteCommand(identity, member.Email, null, null, "Manga", true),
            CancellationToken.None);
        var read = await getHandler.Handle(
            new GetMyGenreFavoritesQuery(identity, member.Email, null, null),
            CancellationToken.None);

        added.IsError.Should().BeFalse();
        duplicate.IsError.Should().BeFalse();
        read.IsError.Should().BeFalse();
        added.Value.Genres.Should().Equal("Manga");
        duplicate.Value.Genres.Should().Equal("Manga");
        read.Value.Genres.Should().Equal("Manga");
        (await fixture.Context.MemberGenreFavorites.CountAsync()).Should().Be(1);

        var removed = await setHandler.Handle(
            new SetMyGenreFavoriteCommand(identity, member.Email, null, null, "Manga", false),
            CancellationToken.None);

        removed.IsError.Should().BeFalse();
        removed.Value.Genres.Should().BeEmpty();
        (await fixture.Context.MemberGenreFavorites.ToArrayAsync()).Should().BeEmpty();
    }
}
