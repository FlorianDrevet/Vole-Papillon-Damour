using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Commands.AddRareBookPhoto;
using Vole_Papillon_Damour.Application.RareBooks.Commands.CreateRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBookPhoto;
using Vole_Papillon_Damour.Application.RareBooks.Commands.MarkRareBookSold;
using Vole_Papillon_Damour.Application.RareBooks.Commands.PublishRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.ReorderRareBookPhotos;
using Vole_Papillon_Damour.Application.RareBooks.Commands.RestoreRareBookAvailability;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UnpublishRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBookPhotoCaption;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.RareBooks;

public sealed class RareBookCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_the_fixed_price_without_calculating_a_total()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();

        var result = await new CreateRareBookCommandHandler(fixture.Context, fixture.Clock)
            .Handle(fixture.CreateCommand(price: 125.50m), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Price.Should().Be(125.50m);
        result.Value.Status.Should().Be(nameof(RareBookStatus.Draft));
        (await fixture.Context.RareBooks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_rejects_an_isbn_already_used_by_another_rare_book()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        await fixture.AddRareBookAsync("Premier titre", isbn13: "9780306406157");

        var result = await new CreateRareBookCommandHandler(fixture.Context, fixture.Clock)
            .Handle(fixture.CreateCommand("Second titre", isbn13: "9780306406157"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RareBook.DuplicateIsbn");
    }

    [Fact]
    public async Task Create_with_a_client_gesture_is_idempotent_for_an_offline_retry()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var clientGestureId = Guid.NewGuid();
        var command = fixture.CreateCommand() with {ClientGestureId = clientGestureId};
        var handler = new CreateRareBookCommandHandler(fixture.Context, fixture.Clock);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.IsError.Should().BeFalse();
        second.IsError.Should().BeFalse();
        second.Value.Id.Should().Be(first.Value.Id);
        (await fixture.Context.RareBooks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Update_keeps_the_original_slug_and_requires_the_current_row_version()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Titre original");
        var originalSlug = book.Slug.Value;
        var rowVersion = book.RowVersion.ToArray();

        var result = await new UpdateRareBookCommandHandler(fixture.Context, fixture.Clock)
            .Handle(fixture.UpdateCommand(book.Id, "Titre modifié", rowVersion), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Slug.Should().Be(originalSlug);
        result.Value.Title.Should().Be("Titre modifié");

        var staleResult = await new UpdateRareBookCommandHandler(fixture.Context, fixture.Clock)
            .Handle(fixture.UpdateCommand(book.Id, "Autre titre", [99]), CancellationToken.None);

        staleResult.IsError.Should().BeTrue();
        staleResult.FirstError.Code.Should().Be("RareBook.ConcurrencyConflict");
    }

    [Fact]
    public async Task Publish_and_unpublish_change_publication_state_and_report_photo_warning()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Édition à publier");
        var publishResult = await new PublishRareBookCommandHandler(fixture.Context, fixture.Clock)
            .Handle(new(book.Id, fixture.UserId), CancellationToken.None);

        publishResult.IsError.Should().BeFalse();
        publishResult.Value.RareBook.Status.Should().Be(nameof(RareBookStatus.Published));
        publishResult.Value.Warnings.Should().ContainSingle();

        var unpublishResult = await new UnpublishRareBookCommandHandler(fixture.Context, fixture.Clock)
            .Handle(new(book.Id, fixture.UserId), CancellationToken.None);

        unpublishResult.IsError.Should().BeFalse();
        unpublishResult.Value.Status.Should().Be(nameof(RareBookStatus.Draft));
    }

    [Fact]
    public async Task Publish_refuses_a_draft_with_no_positive_price()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Prix à corriger", price: 0m);

        var result = await new PublishRareBookCommandHandler(fixture.Context, fixture.Clock)
            .Handle(new(book.Id, fixture.UserId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RareBook.CannotPublish");
    }

    [Fact]
    public async Task Mark_sold_is_idempotent_for_a_published_book_and_refuses_a_draft()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var outbox = Substitute.For<IBookAlertOutbox>();
        var published = await fixture.AddRareBookAsync("Vendu", published: true);
        var command = new MarkRareBookSoldCommand(
            published.Id,
            null,
            null,
            fixture.Now.AddMinutes(2),
            fixture.UserId);

        var first = await new MarkRareBookSoldCommandHandler(fixture.Context, fixture.Clock, outbox, fixture.CreateCheckoutPassageRecorder())
            .Handle(command, CancellationToken.None);
        var second = await new MarkRareBookSoldCommandHandler(fixture.Context, fixture.Clock, outbox, fixture.CreateCheckoutPassageRecorder())
            .Handle(command, CancellationToken.None);

        first.IsError.Should().BeFalse();
        first.Value.IsSold.Should().BeTrue();
        second.IsError.Should().BeFalse();
        second.Value.IsSold.Should().BeTrue();

        var draft = await fixture.AddRareBookAsync("Brouillon");
        var refused = await new MarkRareBookSoldCommandHandler(fixture.Context, fixture.Clock, outbox, fixture.CreateCheckoutPassageRecorder())
            .Handle(command with { RareBookId = draft.Id }, CancellationToken.None);

        refused.IsError.Should().BeTrue();
        refused.FirstError.Code.Should().Be("RareBook.CannotSellDraft");
        await outbox.Received(1).QueueRareBookSoldAsync(
            published.Id,
            command.OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mark_sold_with_passage_records_one_rare_purchase_line_on_retry()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var outbox = Substitute.For<IBookAlertOutbox>();
        var published = await fixture.AddRareBookAsync("Vente associée", published: true);
        var passageId = Guid.NewGuid();
        var command = new MarkRareBookSoldCommand(
            published.Id,
            null,
            null,
            fixture.Now.AddMinutes(2),
            fixture.UserId) with { CheckoutPassageId = passageId };
        var handler = new MarkRareBookSoldCommandHandler(
            fixture.Context, fixture.Clock, outbox, fixture.CreateCheckoutPassageRecorder());

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        first.IsError.Should().BeFalse();
        replay.IsError.Should().BeFalse();
        (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(1);
        var line = await fixture.Context.CheckoutPassageLines.SingleAsync();
        line.RareBookId.Should().Be(published.Id);
    }

    [Fact]
    public async Task Mark_sold_rolls_back_when_the_alert_outbox_cannot_be_written()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var outbox = Substitute.For<IBookAlertOutbox>();
        var published = await fixture.AddRareBookAsync("Alerte atomique", published: true);
        var command = new MarkRareBookSoldCommand(
            published.Id,
            null,
            null,
            fixture.Now.AddMinutes(2),
            fixture.UserId);
        outbox.QueueRareBookSoldAsync(
                published.Id,
                command.OccurredAt,
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("outbox unavailable"));

        var handler = new MarkRareBookSoldCommandHandler(fixture.Context, fixture.Clock, outbox, fixture.CreateCheckoutPassageRecorder());

        await FluentActions.Invoking(() => handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.RareBooks.SingleAsync(book => book.Id == published.Id))
            .IsSold.Should().BeFalse();
    }

    [Fact]
    public async Task Restore_availability_is_allowed_only_in_the_short_correction_window()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var outbox = Substitute.For<IBookAlertOutbox>();
        var recent = await fixture.AddRareBookAsync("Correction récente", published: true);
        recent.MarkSold(fixture.Now.AddSeconds(-5), null, null, fixture.UserId);
        await fixture.Context.SaveChangesAsync();

        var restored = await new RestoreRareBookAvailabilityCommandHandler(fixture.Context, fixture.Clock, outbox)
            .Handle(new(recent.Id, fixture.UserId), CancellationToken.None);

        restored.IsError.Should().BeFalse();
        restored.Value.IsSold.Should().BeFalse();

        var expired = await fixture.AddRareBookAsync("Correction trop tardive", published: true);
        expired.MarkSold(fixture.Now.AddSeconds(-31), null, null, fixture.UserId);
        await fixture.Context.SaveChangesAsync();

        var refused = await new RestoreRareBookAvailabilityCommandHandler(fixture.Context, fixture.Clock, outbox)
            .Handle(new(expired.Id, fixture.UserId), CancellationToken.None);

        refused.IsError.Should().BeTrue();
        refused.FirstError.Code.Should().Be("RareBook.RestoreWindowExpired");
        await outbox.Received(1).CancelPendingForRareBookAsync(
            recent.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_availability_rolls_back_when_alert_cancellation_cannot_be_written()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var outbox = Substitute.For<IBookAlertOutbox>();
        var published = await fixture.AddRareBookAsync("Restauration atomique", published: true);
        published.MarkSold(fixture.Now.AddSeconds(-5), null, null, fixture.UserId);
        await fixture.Context.SaveChangesAsync();
        outbox.CancelPendingForRareBookAsync(
                published.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("outbox unavailable")));

        var handler = new RestoreRareBookAvailabilityCommandHandler(fixture.Context, fixture.Clock, outbox);

        await FluentActions.Invoking(() => handler.Handle(
                new(published.Id, fixture.UserId),
                CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.RareBooks.SingleAsync(book => book.Id == published.Id))
            .IsSold.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_removes_photo_blobs_before_removing_the_rare_book()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Avec photo");
        var photo = await fixture.AddPhotoAsync(book);
        fixture.Blob.DeleteFileAsync(BlobContainer.RareBookPhotos, photo.BlobName)
            .Returns(photo.BlobName);

        var result = await new DeleteRareBookCommandHandler(fixture.Context, fixture.Blob, fixture.Clock)
            .Handle(new(book.Id, fixture.UserId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.RareBooks.CountAsync()).Should().Be(0);
        (await fixture.Context.RareBookPhotos.CountAsync()).Should().Be(0);
        (await fixture.Context.RareBookTombstones.SingleAsync()).RareBookId.Should().Be(book.Id.Value);
        await fixture.Blob.Received(1).DeleteFileAsync(
            BlobContainer.RareBookPhotos,
            photo.BlobName);
    }

    [Fact]
    public async Task Delete_keeps_the_row_when_blob_deletion_fails()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Blob manquant");
        var photo = await fixture.AddPhotoAsync(book);
        fixture.Blob.DeleteFileAsync(BlobContainer.RareBookPhotos, photo.BlobName)
            .Returns(string.Empty);

        var result = await new DeleteRareBookCommandHandler(fixture.Context, fixture.Blob, fixture.Clock)
            .Handle(new(book.Id, fixture.UserId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        (await fixture.Context.RareBooks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Add_photo_uploads_to_the_rare_book_blob_service_and_appends_position()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Galerie");
        fixture.Blob.UploadRareBookPhotoAsync(Arg.Any<string>(), Arg.Any<Stream>())
            .Returns(call => new Uri($"https://storage.test/{call.Arg<string>()}"));

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await new AddRareBookPhotoCommandHandler(
                fixture.Context,
                fixture.Blob,
                fixture.Clock)
            .Handle(
                new AddRareBookPhotoCommand(
                    book.Id,
                    stream,
                    "couverture.png",
                    "image/png",
                    stream.Length,
                    "Face",
                    fixture.UserId),
                CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Photos.Should().ContainSingle();
        result.Value.Photos[0].Position.Should().Be(0);
        result.Value.Photos[0].BlobName.Should().StartWith($"{book.Id.Value:D}/");
        await fixture.Blob.Received(1).UploadRareBookPhotoAsync(
            Arg.Is<string>(name => name.EndsWith(".png", StringComparison.Ordinal)),
            Arg.Any<Stream>());
    }

    [Fact]
    public async Task Add_photo_rejects_an_unsupported_type_before_uploading()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Format invalide");
        await using var stream = new MemoryStream([1]);

        var result = await new AddRareBookPhotoCommandHandler(
                fixture.Context,
                fixture.Blob,
                fixture.Clock)
            .Handle(new(book.Id, stream, "image.gif", "image/gif", 1, null, fixture.UserId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RareBook.InvalidPhotoType");
        await fixture.Blob.DidNotReceive().UploadRareBookPhotoAsync(Arg.Any<string>(), Arg.Any<Stream>());
    }

    [Fact]
    public async Task Reorder_requires_an_exact_permutation_and_updates_positions()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Ordre");
        var first = await fixture.AddPhotoAsync(book, "first.jpg");
        var second = await fixture.AddPhotoAsync(book, "second.jpg");

        var result = await new ReorderRareBookPhotosCommandHandler(fixture.Context, fixture.Clock)
            .Handle(new(book.Id, [second.Id, first.Id], fixture.UserId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Photos.Select(photo => photo.Id).Should().ContainInOrder(second.Id.Value, first.Id.Value);
        result.Value.Photos.Select(photo => photo.Position).Should().Equal(0, 1);

        var refused = await new ReorderRareBookPhotosCommandHandler(fixture.Context, fixture.Clock)
            .Handle(new(book.Id, [first.Id, first.Id], fixture.UserId), CancellationToken.None);

        refused.IsError.Should().BeTrue();
        refused.FirstError.Code.Should().Be("RareBook.InvalidPhotoOrder");
    }

    [Fact]
    public async Task Caption_update_and_photo_delete_keep_the_gallery_consistent()
    {
        await using var fixture = await RareBookFeatureTestFixture.CreateAsync();
        var book = await fixture.AddRareBookAsync("Légendes");
        var photo = await fixture.AddPhotoAsync(book);
        fixture.Blob.DeleteFileAsync(BlobContainer.RareBookPhotos, photo.BlobName)
            .Returns(photo.BlobName);

        var captionResult = await new UpdateRareBookPhotoCaptionCommandHandler(fixture.Context, fixture.Clock)
            .Handle(new(photo.Id, "Nouvelle légende", fixture.UserId), CancellationToken.None);
        var deleteResult = await new DeleteRareBookPhotoCommandHandler(
                fixture.Context,
                fixture.Blob,
                fixture.Clock)
            .Handle(new(photo.Id, fixture.UserId), CancellationToken.None);

        captionResult.IsError.Should().BeFalse();
        captionResult.Value.Photos[0].Caption.Should().Be("Nouvelle légende");
        deleteResult.IsError.Should().BeFalse();
        deleteResult.Value.Photos.Should().BeEmpty();
        (await fixture.Context.RareBookPhotos.CountAsync()).Should().Be(0);
    }
}
