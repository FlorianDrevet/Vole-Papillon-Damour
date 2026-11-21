using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Admin;

public sealed class WithdrawBookCommandHandlerTests
{
    private static readonly UserId Administrator =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Handle_AcceptsAnInternalCode()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddInternalCodeBookAsync(quantityAvailable: 2);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new WithdrawBookCommandHandler(
            fixture.Context,
            clock,
            new NotFoundReportLapser(fixture.Context));

        var result = await handler.Handle(
            new WithdrawBookCommand("2000000000015", 1, "Retrait pour don", Administrator),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be("2000000000015");
        result.Value.QuantityAvailable.Should().Be(1);
    }
}
