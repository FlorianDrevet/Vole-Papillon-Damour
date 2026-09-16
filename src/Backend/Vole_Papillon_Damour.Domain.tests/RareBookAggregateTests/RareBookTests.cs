using FluentAssertions;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.RareBookAggregateTests;

public sealed class RareBookTests
{
    private static readonly DateTime CreatedAt = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UpdatedAt = CreatedAt.AddMinutes(5);
    private static readonly DateTime SoldAt = CreatedAt.AddMinutes(10);
    private static readonly UserId CreatedBy = UserId.Create(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly UserId UpdatedBy = UserId.Create(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    [Fact]
    public void Create_WithValidFields_InitializesDraftWithFixedSlug()
    {
        var isbn = CreateIsbn();

        var rareBook = RareBook.Create(
            title: "Les Fables de la Fontaine",
            authorMention: "Gustave Doré",
            publisher: "Hachette",
            publicationYear: 1868,
            shelf: RareBookShelf.Create("Éditions anciennes"),
            price: 35.00m,
            condition: RareBookCondition.AsNew,
            publicDescription: "Demi-cuir à coins.",
            binding: "Demi-cuir à coins",
            dimensions: "32 × 44 cm",
            pageCount: 300,
            shelfLocation: "Table rares · caisse 2",
            priceSetBy: "Conseil du 5 mars",
            createdAt: CreatedAt,
            createdBy: CreatedBy,
            isbn13: isbn);

        rareBook.Id.Value.Should().NotBeEmpty();
        rareBook.Isbn13.Should().Be(isbn);
        rareBook.Title.Should().Be("Les Fables de la Fontaine");
        rareBook.Slug.Value.Should().Be("les-fables-de-la-fontaine-gustave-dore-1868");
        rareBook.Status.Should().Be(RareBookStatus.Draft);
        rareBook.IsSold.Should().BeFalse();
        rareBook.Price.Should().Be(35.00m);
        rareBook.Photos.Should().BeEmpty();
        rareBook.CreatedAt.Should().Be(CreatedAt);
        rareBook.UpdatedAt.Should().Be(CreatedAt);
        rareBook.CreatedBy.Should().Be(CreatedBy);
        rareBook.UpdatedBy.Should().Be(CreatedBy);
    }

    [Fact]
    public void Create_WithNegativePrice_Throws()
    {
        var action = () => CreateRareBook(price: -1m);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Publish_WithPositivePriceAndTitle_PublishesOnce()
    {
        var rareBook = CreateRareBook();

        var published = rareBook.Publish(UpdatedAt);
        var publishedAgain = rareBook.Publish(SoldAt);

        published.Should().BeTrue();
        publishedAgain.Should().BeFalse();
        rareBook.Status.Should().Be(RareBookStatus.Published);
        rareBook.UpdatedAt.Should().Be(UpdatedAt);
    }

    [Fact]
    public void Publish_WithoutPhoto_IsAllowed()
    {
        var rareBook = CreateRareBook();

        var published = rareBook.Publish(UpdatedAt);

        published.Should().BeTrue();
        rareBook.Photos.Should().BeEmpty();
    }

    [Fact]
    public void Publish_WithEmptyTitleOrZeroPrice_ReturnsFalse()
    {
        var emptyTitle = CreateRareBook(title: " ");
        var zeroPrice = CreateRareBook(price: 0m);

        var emptyTitleResult = emptyTitle.Publish(UpdatedAt);
        var zeroPriceResult = zeroPrice.Publish(UpdatedAt);

        emptyTitleResult.Should().BeFalse();
        zeroPriceResult.Should().BeFalse();
        emptyTitle.Status.Should().Be(RareBookStatus.Draft);
        zeroPrice.Status.Should().Be(RareBookStatus.Draft);
    }

    [Fact]
    public void Unpublish_RemovesTheBookFromThePublicStatus()
    {
        var rareBook = CreateRareBook();
        rareBook.Publish(UpdatedAt, UpdatedBy).Should().BeTrue();

        rareBook.Unpublish(SoldAt, CreatedBy).Should().BeTrue();

        rareBook.Status.Should().Be(RareBookStatus.Draft);
        rareBook.UpdatedAt.Should().Be(SoldAt);
        rareBook.UpdatedBy.Should().Be(CreatedBy);
        rareBook.Unpublish(SoldAt.AddMinutes(1), CreatedBy).Should().BeFalse();
    }

    [Fact]
    public void MarkSold_WhenDraft_Throws()
    {
        var rareBook = CreateRareBook();

        var action = () => rareBook.MarkSold(SoldAt);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkSold_WhenPublished_SetsTraceabilityAndIsIdempotent()
    {
        var rareBook = CreateRareBook();
        rareBook.Publish(UpdatedAt).Should().BeTrue();
        var fairId = AssoEventsId.Create(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var sessionId = ScanSessionId.Create(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

        var sold = rareBook.MarkSold(SoldAt, fairId, sessionId);
        var soldAgain = rareBook.MarkSold(SoldAt.AddMinutes(1));

        sold.Should().BeTrue();
        soldAgain.Should().BeFalse();
        rareBook.IsSold.Should().BeTrue();
        rareBook.SoldAt.Should().Be(SoldAt);
        rareBook.SoldAtFairId.Should().Be(fairId);
        rareBook.SoldInSessionId.Should().Be(sessionId);
        rareBook.UpdatedAt.Should().Be(SoldAt);
    }

    [Fact]
    public void MarkSold_WithSeparateAuditTime_UsesTheApplicationClockForUpdatedAt()
    {
        var rareBook = CreateRareBook();
        rareBook.Publish(UpdatedAt, UpdatedBy).Should().BeTrue();

        rareBook.MarkSold(
                SoldAt,
                null,
                null,
                UpdatedAt.AddMinutes(1),
                CreatedBy)
            .Should().BeTrue();

        rareBook.SoldAt.Should().Be(SoldAt);
        rareBook.UpdatedAt.Should().Be(UpdatedAt.AddMinutes(1));
        rareBook.UpdatedBy.Should().Be(CreatedBy);
    }

    [Fact]
    public void RestoreAvailability_WithinThirtySeconds_ClearsSaleTraceability()
    {
        var rareBook = CreateRareBook();
        rareBook.Publish(UpdatedAt, UpdatedBy).Should().BeTrue();
        rareBook.MarkSold(SoldAt.AddSeconds(-5), null, null, UpdatedBy).Should().BeTrue();

        var restored = rareBook.RestoreAvailability(SoldAt, CreatedBy);

        restored.Should().BeTrue();
        rareBook.IsSold.Should().BeFalse();
        rareBook.SoldAt.Should().BeNull();
        rareBook.SoldAtFairId.Should().BeNull();
        rareBook.SoldInSessionId.Should().BeNull();
        rareBook.UpdatedAt.Should().Be(SoldAt);
        rareBook.UpdatedBy.Should().Be(CreatedBy);
    }

    [Fact]
    public void RestoreAvailability_AfterThirtySeconds_Throws()
    {
        var rareBook = CreateRareBook();
        rareBook.Publish(UpdatedAt, UpdatedBy).Should().BeTrue();
        rareBook.MarkSold(SoldAt.AddSeconds(-31), null, null, UpdatedBy).Should().BeTrue();

        var action = () => rareBook.RestoreAvailability(SoldAt, CreatedBy);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddPhoto_AssignsContiguousPositions()
    {
        var rareBook = CreateRareBook();
        var first = CreatePhoto(rareBook, "first.jpg");
        var second = CreatePhoto(rareBook, "second.webp", contentType: "image/webp");

        rareBook.AddPhoto(first, UpdatedAt).Should().BeTrue();
        rareBook.AddPhoto(second, SoldAt).Should().BeTrue();

        rareBook.Photos.Select(photo => photo.Id).Should().ContainInOrder(first.Id, second.Id);
        rareBook.Photos.Select(photo => photo.Position).Should().Equal(0, 1);
    }

    [Fact]
    public void ReorderPhotos_WithExactPermutation_UpdatesPositions()
    {
        var rareBook = CreateRareBook();
        var first = CreatePhoto(rareBook, "first.jpg");
        var second = CreatePhoto(rareBook, "second.jpg");
        rareBook.AddPhoto(first, UpdatedAt).Should().BeTrue();
        rareBook.AddPhoto(second, UpdatedAt.AddMinutes(1)).Should().BeTrue();

        var reordered = rareBook.ReorderPhotos(new[] { second.Id, first.Id }, SoldAt);

        reordered.Should().BeTrue();
        rareBook.Photos.Select(photo => photo.Id).Should().ContainInOrder(second.Id, first.Id);
        rareBook.Photos.Select(photo => photo.Position).Should().Equal(0, 1);
        rareBook.UpdatedAt.Should().Be(SoldAt);
    }

    [Fact]
    public void ReorderPhotos_WithMissingOrDuplicateId_Throws()
    {
        var rareBook = CreateRareBook();
        var first = CreatePhoto(rareBook, "first.jpg");
        var second = CreatePhoto(rareBook, "second.jpg");
        rareBook.AddPhoto(first, UpdatedAt).Should().BeTrue();
        rareBook.AddPhoto(second, UpdatedAt.AddMinutes(1)).Should().BeTrue();

        var missing = () => rareBook.ReorderPhotos(new[] { first.Id }, SoldAt);
        var duplicate = () => rareBook.ReorderPhotos(new[] { first.Id, first.Id }, SoldAt);

        missing.Should().Throw<ArgumentException>();
        duplicate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddPhoto_FromAnotherRareBook_Throws()
    {
        var rareBook = CreateRareBook();
        var otherRareBook = CreateRareBook();
        var photo = CreatePhoto(otherRareBook, "foreign.jpg");

        var action = () => rareBook.AddPhoto(photo, UpdatedAt);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PhotoMutations_UpdateTheAggregateAuditUser()
    {
        var rareBook = CreateRareBook();
        var photo = CreatePhoto(rareBook, "cover.jpg");

        rareBook.AddPhoto(photo, UpdatedAt, UpdatedBy).Should().BeTrue();
        rareBook.UpdatedBy.Should().Be(UpdatedBy);

        rareBook.UpdatePhotoCaption(photo.Id, "Page de titre", SoldAt, CreatedBy)
            .Should().BeTrue();

        photo.Caption.Should().Be("Page de titre");
        rareBook.UpdatedAt.Should().Be(SoldAt);
        rareBook.UpdatedBy.Should().Be(CreatedBy);
    }

    private static RareBook CreateRareBook(
        string title = "Les Fables de la Fontaine",
        decimal price = 35m)
    {
        return RareBook.Create(
            title: title,
            price: price,
            createdAt: CreatedAt,
            createdBy: CreatedBy,
            authorMention: "Gustave Doré",
            publicationYear: 1868,
            shelf: RareBookShelf.Create("Éditions anciennes"),
            condition: RareBookCondition.AsNew);
    }

    private static RareBookPhoto CreatePhoto(
        RareBook rareBook,
        string blobName,
        string contentType = "image/jpeg")
    {
        return RareBookPhoto.Create(
            rareBook.Id,
            new Uri($"https://storage.example.test/livres-rares/{blobName}"),
            blobName,
            contentType,
            1024,
            CreatedAt,
            CreatedBy);
    }

    private static Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13 CreateIsbn()
    {
        Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13.TryCreate(
            "9782070363735",
            out var isbn).Should().BeTrue();
        return isbn;
    }
}
