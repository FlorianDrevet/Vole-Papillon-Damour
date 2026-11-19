using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Admin;

public sealed class CreateBookWithoutIsbnCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000042"));

    [Fact]
    public async Task Handle_CreatesBookWithInternalCodeAndEntryMovement()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(
            new CreateBookWithoutIsbnCommand("Atlas du Maroc", 3, Volunteer, Authors: "Anonyme", Publisher: "Éditions Test"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var code = result.Value.Isbn13;
        BookCode.IsValidInternalCode(code).Should().BeTrue();
        result.Value.QuantityAvailable.Should().Be(3);

        var bookCode = BookCode.CreateInternal(code);
        var book = await fixture.Context.Books.SingleAsync(candidate => candidate.Id == bookCode);
        book.IsInternalCode.Should().BeTrue();
        book.Title.Should().Be("Atlas du Maroc");
        book.Authors.Should().Be("Anonyme");

        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.DirectEntry);
        movement.Quantity.Should().Be(3);
        movement.Isbn13.Value.Should().Be(code);
    }

    [Fact]
    public async Task Handle_IssuesDistinctCodesForSuccessiveCreations()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = CreateHandler(fixture);

        var first = await handler.Handle(new CreateBookWithoutIsbnCommand("Premier", 1, Volunteer), CancellationToken.None);
        var second = await handler.Handle(new CreateBookWithoutIsbnCommand("Second", 1, Volunteer), CancellationToken.None);

        first.Value.Isbn13.Should().NotBe(second.Value.Isbn13);
        (await fixture.Context.InternalBookCodeSequences.SingleAsync()).LastIssuedNumber.Should().Be(2);
    }

    [Fact]
    public async Task Handle_RequiresATitle()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(new CreateBookWithoutIsbnCommand("   ", 1, Volunteer), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidTitle");
        (await fixture.Context.Books.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_RejectsAQuantityBelowOne()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(new CreateBookWithoutIsbnCommand("Titre", 0, Volunteer), CancellationToken.None);

        result.IsError.Should().BeTrue();
        (await fixture.Context.Books.CountAsync()).Should().Be(0);
    }

    private static CreateBookWithoutIsbnCommandHandler CreateHandler(ScanBookFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new CreateBookWithoutIsbnCommandHandler(fixture.Context, clock);
    }
}