using FluentAssertions;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetScanCandidate;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Queries;

public sealed class ScanCandidateQueryHandlerTests
{
    private const string Isbn = "9782070612758";

    [Fact]
    public async Task ScanCandidate_ReturnsOldestRequesterContact()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var oldestRequester = await fixture.AddRequesterAsync("Martin", "Jeanne", "0612345678");
        var oldestRequest = await fixture.AddEditionRequestAsync(
            oldestRequester.Id, Isbn, ManualRequestFixture.DefaultNow.AddMinutes(-5));
        var newerRequester = await fixture.AddRequesterAsync("Durand", "Alice", "0698765432");
        await fixture.AddEditionRequestAsync(newerRequester.Id, Isbn);
        var handler = new GetManualRequestScanCandidateQueryHandler(fixture.Context);

        var result = await handler.Handle(new GetManualRequestScanCandidateQuery(Isbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeNull();
        result.Value!.RequestId.Should().Be(oldestRequest.Id);
        result.Value.FirstName.Should().Be("Jeanne");
        result.Value.LastName.Should().Be("Martin");
        result.Value.Phone.Should().Be("0612345678");
    }

    [Fact]
    public async Task ScanCandidate_ErasedRequester_ReturnsNull()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        await fixture.AddEditionRequestAsync(requester.Id, Isbn);
        requester.Erase(ManualRequestFixture.DefaultNow.AddMinutes(1));
        await fixture.Context.SaveChangesAsync();
        var handler = new GetManualRequestScanCandidateQueryHandler(fixture.Context);

        var result = await handler.Handle(new GetManualRequestScanCandidateQuery(Isbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeNull();
    }
}
