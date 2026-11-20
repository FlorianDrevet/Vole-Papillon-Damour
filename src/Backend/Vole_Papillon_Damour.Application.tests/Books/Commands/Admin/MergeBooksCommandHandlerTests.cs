using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Admin;

public sealed class MergeBooksCommandHandlerTests
{
    private static readonly UserId Administrator =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Handle_RedirectsAnInternalCodeToTheIsbnEdition()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddInternalCodeBookAsync(quantityAvailable: 1);
        var target = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new MergeBooksCommand("2000000000015", "9782070363735", "Doublon sans ISBN", Administrator),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var source = await fixture.Context.Books
            .SingleAsync(book => book.Id == BookCode.CreateInternal("2000000000015"));
        source.RedirectedToIsbn13.Should().Be(target.Id);
    }

    [Fact]
    public async Task Handle_RefusesAnInternalCodeAsRedirectTarget()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        await fixture.AddInternalCodeBookAsync(quantityAvailable: 0);
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new MergeBooksCommand("9782070363735", "2000000000015", "Doublon sans ISBN", Administrator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidIsbn");
    }

    private static MergeBooksCommandHandler CreateHandler(ScanBookFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        return new MergeBooksCommandHandler(
            fixture.Context,
            clock,
            new NotFoundReportLapser(fixture.Context));
    }
}
