using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.AddBookPhoto;
using Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.DeleteBookPhoto;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Books.Queries.GetBookPhotos;
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
    private const string NotFoundIsbn = "9782070368228";

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
    public async Task AddPhoto_RefusesABookWithUnavailableMetadata()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate(NotFoundIsbn, out var isbn).Should().BeTrue();
        var book = Book.Create(isbn, Now);
        book.RecordMetadataProviderFailure(Now);
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(NotFoundIsbn, "image/jpeg"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.PhotoNotAllowed");
    }

    [Fact]
    public async Task AddPhoto_CreatesANotFoundBookForANewUnknownIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var blob = CreateBlob();

        var result = await CreateAddHandler(fixture, blob).Handle(
            CreateAddCommand(NotFoundIsbn, "image/jpeg"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Url.Should().StartWith("https://blob.test/isbn-non-trouve/" + NotFoundIsbn + "/");
        var book = await fixture.Context.Books.Include(candidate => candidate.Photos).SingleAsync();
        book.MetadataStatus.Should().Be(BookMetadataStatus.NotFound);
        book.Photos.Should().ContainSingle();
    }

    [Fact]
    public async Task AddPhoto_RejectsTheSixthPhotoForANotFoundIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate(NotFoundIsbn, out var isbn).Should().BeTrue();
        var book = Book.Create(isbn, Now);
        book.RecordMetadataNotFound(Now);
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();

        var handler = CreateAddHandler(fixture, CreateBlob());
        for (var index = 0; index < 5; index++)
        {
            var added = await handler.Handle(
                CreateAddCommand(NotFoundIsbn, "image/jpeg"),
                CancellationToken.None);
            added.IsError.Should().BeFalse();
        }

        var result = await handler.Handle(
            CreateAddCommand(NotFoundIsbn, "image/jpeg"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.TooManyPhotos");
    }

    [Fact]
    public async Task GetAdminBook_IncludesPhotosForAnUnfoundIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var added = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(NotFoundIsbn, "image/jpeg"),
            CancellationToken.None);
        added.IsError.Should().BeFalse();
        fixture.Context.ChangeTracker.Clear();

        var result = await new GetAdminBookQueryHandler(fixture.Context).Handle(
            new GetAdminBookQuery(NotFoundIsbn),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Photos.Should().ContainSingle(photo => photo.Url == added.Value.Url);
    }

    [Fact]
    public async Task GetPhotos_ReturnsPhotosInPositionOrderForAnUnfoundIsbn()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var addHandler = CreateAddHandler(fixture, CreateBlob());
        (await addHandler.Handle(CreateAddCommand(NotFoundIsbn, "image/jpeg"), CancellationToken.None))
            .IsError.Should().BeFalse();
        (await addHandler.Handle(CreateAddCommand(NotFoundIsbn, "image/png"), CancellationToken.None))
            .IsError.Should().BeFalse();
        fixture.Context.ChangeTracker.Clear();

        var result = await new GetBookPhotosQueryHandler(fixture.Context).Handle(
            new GetBookPhotosQuery(NotFoundIsbn),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Select(photo => photo.Position).Should().Equal(0, 1);
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
    public async Task AddPhoto_AfterADeletion_DoesNotReuseAnExistingPosition()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        fixture.Context.Books.Add(Book.CreateWithInternalCode(BookCode.CreateInternal(InternalCode), Now));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var first = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(InternalCode, "image/png"),
            CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();
        await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(InternalCode, "image/png"),
            CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();
        await new DeleteBookPhotoCommandHandler(fixture.Context, CreateBlob(), CreateClock())
            .Handle(new DeleteBookPhotoCommand(InternalCode, first.Value.Id, Volunteer), CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();

        var third = await CreateAddHandler(fixture, CreateBlob()).Handle(
            CreateAddCommand(InternalCode, "image/png"),
            CancellationToken.None);

        third.IsError.Should().BeFalse();
        third.Value.Position.Should().Be(2);
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
