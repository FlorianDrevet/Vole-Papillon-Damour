using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests;

public sealed class BookPhotoTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AddPhoto_AcceptsPhotosOfABookWithoutIsbn()
    {
        var book = Book.CreateWithInternalCode(BookCode.CreateInternal("2000000000015"), Now);

        book.AddPhoto(CreatePhoto(book.Id, 0), Now);

        book.Photos.Should().HaveCount(1);
    }

    [Fact]
    public void AddPhoto_RejectsPhotosOfABookWithAnIsbn()
    {
        Isbn13.TryCreate("9782070409228", out var isbn).Should().BeTrue();
        var book = Book.Create(isbn, Now);

        var add = () => book.AddPhoto(CreatePhoto(book.Id, 0), Now);

        add.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddPhoto_StopsAtTheMaximumNumberOfPhotos()
    {
        var book = Book.CreateWithInternalCode(BookCode.CreateInternal("2000000000015"), Now);
        for (var position = 0; position < BookPhoto.MaximumPerBook; position++)
        {
            book.AddPhoto(CreatePhoto(book.Id, position), Now);
        }

        var add = () => book.AddPhoto(CreatePhoto(book.Id, BookPhoto.MaximumPerBook), Now);

        add.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemovePhoto_ReportsWhetherAPhotoWasRemoved()
    {
        var book = Book.CreateWithInternalCode(BookCode.CreateInternal("2000000000015"), Now);
        var photo = CreatePhoto(book.Id, 0);
        book.AddPhoto(photo, Now);

        book.RemovePhoto(photo.Id, Now).Should().BeTrue();
        book.RemovePhoto(photo.Id, Now).Should().BeFalse();
        book.Photos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    public void Create_RejectsUnsupportedContentTypes(string contentType)
    {
        var create = () => BookPhoto.Create(
            Guid.NewGuid(),
            BookCode.CreateInternal("2000000000015"),
            new Uri("https://blob.test/photo.gif"),
            "photo.gif",
            0,
            contentType,
            1024,
            Now,
            UserId.Create(Guid.NewGuid()));

        create.Should().Throw<ArgumentException>();
    }

    private static BookPhoto CreatePhoto(BookCode code, int position) => BookPhoto.Create(
        Guid.NewGuid(),
        code,
        new Uri($"https://blob.test/photo-{position}.jpg"),
        $"sans-isbn/{code.Value}/photo-{position}.jpg",
        position,
        "image/jpeg",
        1024,
        Now,
        UserId.Create(Guid.NewGuid()));
}