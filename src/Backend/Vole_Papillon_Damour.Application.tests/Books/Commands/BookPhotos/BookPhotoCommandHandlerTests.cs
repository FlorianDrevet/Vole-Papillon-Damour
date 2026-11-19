using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.AddBookPhoto;
using Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.DeleteBookPhoto;
using Vole_Papillon_Damour.Application.Books.Queries.GetBooksWithoutIsbn;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.BookPhotos;

public sealed class BookPhotoCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000077"));
    private const string InternalCode = "2000000000015";

    [Fact]
    public async Task AddPhoto_StoresThePhotoOfABookWithoutIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        fixture.Context.Books.Add(Book.CreateWithInternalCode(BookCode.CreateInternal(InternalCode), Now));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(InternalCode, "image/jpeg"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Position.Should().Be(0);
        result.Value.Url.Should().StartWith("https://blob.test/sans-isbn/" + InternalCode + "/");
        var book = await fixture.Context.Books.Include(candidate => candidate.Photos)
            .SingleAsync(candidate => candidate.Id == BookCode.CreateInternal(InternalCode));
        book.Photos.Should().ContainSingle();
    }

    [Fact]
    public async Task AddPhoto_RefusesABookWithAnIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070409228", out var isbn).Should().BeTrue();
        fixture.Context.Books.Add(Book.Create(isbn, Now));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand("9782070409228", "image/jpeg"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.PhotoNotAllowed");
    }

    [Fact]
    public async Task AddPhoto_RejectsAnUnsupportedType()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        fixture.Context.Books.Add(Book.CreateWithInternalCode(BookCode.CreateInternal(InternalCode), Now));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(InternalCode, "application/pdf"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidPhotoType");
    }

    [Fact]
    public async Task DeletePhoto_RemovesThePhotoFromTheBook()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        fixture.Context.Books.Add(Book.CreateWithInternalCode(BookCode.CreateInternal(InternalCode), Now));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var added = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(InternalCode, "image/png"),
            CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();

        var deleted = await new DeleteBookPhotoCommandHandler(fixture.Context, CreateBlob(), CreateClock())
            .Handle(new DeleteBookPhotoCommand(InternalCode, added.Value.Id, Volunteer), CancellationToken.None);

        deleted.IsError.Should().BeFalse();
        var book = await fixture.Context.Books.Include(candidate => candidate.Photos)
            .SingleAsync(candidate => candidate.Id == BookCode.CreateInternal(InternalCode));
        book.Photos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBooksWithoutIsbn_ListsOnlyBooksWithoutIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070409228", out var isbn).Should().BeTrue();
        fixture.Context.Books.Add(Book.Create(isbn, Now));
        fixture.Context.Books.Add(Book.CreateWithInternalCode(BookCode.CreateInternal(InternalCode), Now));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await new GetBooksWithoutIsbnQueryHandler(fixture.Context)
            .Handle(new GetBooksWithoutIsbnQuery(1, 20, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().Code.Should().Be(InternalCode);
    }

    private static AddBookPhotoCommandHandler CreateAddHandler(ScanBookFixture fixture, IBlobService blob) =>
        new(fixture.Context, blob, CreateClock());

    private static AddBookPhotoCommand CreateAddCommand(string code, string contentType) => new(
        code,
        new MemoryStream(new byte[] { 1, 2, 3 }),
        "photo.jpg",
        contentType,
        3,
        Volunteer);

    private static IBlobService CreateBlob()
    {
        var blob = Substitute.For<IBlobService>();
        blob.UploadBookPhotoAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new Uri("https://blob.test/" + call.ArgAt<string>(0)));
        return blob;
    }

    private static IDateTimeProvider CreateClock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }
}