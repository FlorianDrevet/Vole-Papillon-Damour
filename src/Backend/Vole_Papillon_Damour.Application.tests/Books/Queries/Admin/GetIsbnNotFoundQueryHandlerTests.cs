using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetIsbnNotFoundQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_PaginatesOnlyNotFoundBooksAndReportsAnExactTotal()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var first = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        first.RecordMetadataNotFound(Now.AddDays(-2));
        var second = await fixture.AddBookAsync("9782070409228", quantityAvailable: 0);
        second.RecordMetadataNotFound(Now.AddDays(-1));
        var failed = await fixture.AddBookAsync("9782070612758", quantityAvailable: 0);
        failed.RecordMetadataProviderFailure(Now, "Fournisseur indisponible.");
        await fixture.AddBookAsync("9782253006329", quantityAvailable: 0);
        var discarded = await fixture.AddBookAsync("9782253004226", quantityAvailable: 0);
        discarded.RecordMetadataNotFound(Now);
        discarded.DiscardMetadataCandidate(Now);
        await fixture.Context.SaveChangesAsync();

        var handler = new GetIsbnNotFoundQueryHandler(fixture.Context);

        var result = await handler.Handle(new GetIsbnNotFoundQuery(1, 1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        first.IsAbsentFromIsbndb.Should().BeFalse("a NotFound status may be recorded without an ISBNdb lookup");
        result.Value.TotalCount.Should().Be(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Isbn13.Should().Be(first.Id.Value);
        result.Value.Items[0].FailureReason.Should().Be("Aucune notice bibliographique trouvée pour cet ISBN.");
        result.Value.Items[0].ResolveAttempts.Should().Be(1);
        result.Value.Items[0].LastAttemptAt.Should().Be(new DateTimeOffset(Now.AddDays(-2), TimeSpan.Zero));
    }
}
